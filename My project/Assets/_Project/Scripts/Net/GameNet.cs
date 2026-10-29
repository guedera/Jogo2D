using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace TypeDuel.Net
{
    public enum MsgType : byte
    {
        // servidor -> cliente
        Welcome = 1,
        Rejected = 2,
        Lobby = 3,
        Start = 4,
        PowerGranted = 5,
        PowerEffect = 6,
        OpponentProgress = 7,
        GameOver = 8,

        // cliente -> servidor
        WordDone = 32,
        Progress = 33,
        UsePower = 34,
        Finished = 35,
    }

    public delegate void ServerMessageHandler(int senderId, MsgType type, BinaryReader reader);
    public delegate void ClientMessageHandler(MsgType type, BinaryReader reader);

    /// <summary>
    /// Camada de rede minima sobre TCP (LAN). O host e tambem um jogador: as mensagens dele passam por uma fila local.
    /// Tudo roda na thread principal: chamar Tick() todo frame.
    /// Formato: [int32 tamanho][byte tipo][payload]. O host tem id 0; clientes recebem 1, 2, ...
    /// </summary>
    public sealed class GameNet
    {
        sealed class Peer
        {
            public int Id;
            public TcpClient Tcp;
            public NetworkStream Stream;
            public byte[] Buffer = new byte[4096];
            public int BufferLength;
            public bool Dead;
        }

        struct LocalMessage
        {
            public bool ToServer;
            public byte[] Body;
        }

        const int MaxFrameBytes = 1 << 20;

        readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
        readonly Queue<LocalMessage> localQueue = new Queue<LocalMessage>();
        readonly byte[] readTmp = new byte[4096];

        TcpListener listener;
        Peer serverPeer;
        Task connectTask;
        TcpClient pendingClient;
        float connectStart;
        int nextId = 1;
        bool disposed;
        bool welcomed;

        public bool IsHost { get; private set; }
        public int LocalId { get; private set; } = -1;

        /// <summary>Host: decide se um novo cliente pode entrar.</summary>
        public Func<bool> CanAccept = () => true;

        /// <summary>Host: um cliente entrou (id).</summary>
        public event Action<int> PeerConnected;
        /// <summary>Host: um cliente saiu (id).</summary>
        public event Action<int> PeerDisconnected;
        /// <summary>Cliente: conectou e recebeu o id.</summary>
        public event Action Connected;
        /// <summary>Cliente: conexao falhou ou caiu (motivo).</summary>
        public event Action<string> Disconnected;

        /// <summary>Somente no host: mensagens vindas dos jogadores (inclusive o proprio host).</summary>
        public event ServerMessageHandler ServerMessage;
        /// <summary>Em todos os jogadores: mensagens vindas do servidor.</summary>
        public event ClientMessageHandler ClientMessage;

        // ---------------------------------------------------------------- inicio

        public void Host(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Start();
            IsHost = true;
            LocalId = 0;
            welcomed = true;
            Debug.Log($"[TD] Hosting on port {port}");
        }

        public void Join(string ip, int port)
        {
            if (!IPAddress.TryParse(ip, out var address))
                throw new FormatException("Invalid IP address");

            pendingClient = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
            connectTask = pendingClient.ConnectAsync(address, port);
            connectStart = Time.realtimeSinceStartup;
            Debug.Log($"[TD] Connecting to {ip}:{port}");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            foreach (var p in peers.Values) CloseTcp(p.Tcp);
            peers.Clear();
            if (serverPeer != null) CloseTcp(serverPeer.Tcp);
            if (pendingClient != null) CloseTcp(pendingClient);
            try { listener?.Stop(); } catch { /* ignorado */ }
            localQueue.Clear();
        }

        static void CloseTcp(TcpClient tcp)
        {
            try { tcp?.Close(); } catch { /* ignorado */ }
        }

        // ---------------------------------------------------------------- envio

        public void SendToServer(MsgType type, Action<BinaryWriter> write = null)
        {
            var frame = Frame(type, write);
            if (IsHost) localQueue.Enqueue(new LocalMessage { ToServer = true, Body = Body(frame) });
            else if (serverPeer != null) Write(serverPeer, frame);
        }

        public void SendToClient(int clientId, MsgType type, Action<BinaryWriter> write = null)
        {
            var frame = Frame(type, write);
            if (clientId == LocalId) localQueue.Enqueue(new LocalMessage { ToServer = false, Body = Body(frame) });
            else if (peers.TryGetValue(clientId, out var peer)) Write(peer, frame);
        }

        /// <summary>Host: envia para todos os jogadores, inclusive o proprio host.</summary>
        public void Broadcast(MsgType type, Action<BinaryWriter> write = null)
        {
            var frame = Frame(type, write);
            localQueue.Enqueue(new LocalMessage { ToServer = false, Body = Body(frame) });
            foreach (var peer in peers.Values) Write(peer, frame);
        }

        static byte[] Frame(MsgType type, Action<BinaryWriter> write)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(0);
                bw.Write((byte)type);
                write?.Invoke(bw);
                bw.Flush();
                var bytes = ms.ToArray();
                int length = bytes.Length - 4;
                bytes[0] = (byte)length;
                bytes[1] = (byte)(length >> 8);
                bytes[2] = (byte)(length >> 16);
                bytes[3] = (byte)(length >> 24);
                return bytes;
            }
        }

        static byte[] Body(byte[] frame)
        {
            var body = new byte[frame.Length - 4];
            Buffer.BlockCopy(frame, 4, body, 0, body.Length);
            return body;
        }

        static void Write(Peer peer, byte[] frame)
        {
            if (peer.Dead) return;
            try { peer.Stream.Write(frame, 0, frame.Length); }
            catch (Exception) { peer.Dead = true; }
        }

        // ---------------------------------------------------------------- loop

        public void Tick()
        {
            if (disposed) return;

            if (IsHost) AcceptNew();
            else if (connectTask != null) PollConnect();

            if (disposed) return;

            var snapshot = new List<Peer>(peers.Values);
            if (serverPeer != null) snapshot.Add(serverPeer);

            foreach (var peer in snapshot)
            {
                if (disposed) return;
                ReadPeer(peer);
            }

            RemoveDead(snapshot);
            DrainLocal();
        }

        void AcceptNew()
        {
            while (!disposed && listener.Pending())
            {
                var tcp = listener.AcceptTcpClient();
                tcp.NoDelay = true;
                tcp.SendTimeout = 1000;

                var peer = new Peer { Tcp = tcp, Stream = tcp.GetStream() };

                if (peers.Count + 1 >= Config.MaxPlayers || !CanAccept())
                {
                    Write(peer, Frame(MsgType.Rejected, w => w.Write("Game is full or already started")));
                    CloseTcp(tcp);
                    continue;
                }

                peer.Id = nextId++;
                peers[peer.Id] = peer;
                Write(peer, Frame(MsgType.Welcome, w => w.Write(peer.Id)));
                Debug.Log($"[TD] Peer {peer.Id} connected");
                PeerConnected?.Invoke(peer.Id);
            }
        }

        void PollConnect()
        {
            if (connectTask.IsCompleted)
            {
                var task = connectTask;
                connectTask = null;

                if (task.IsFaulted || task.IsCanceled || !pendingClient.Connected)
                {
                    Fail("Could not connect to host");
                    return;
                }

                pendingClient.SendTimeout = 1000;
                serverPeer = new Peer { Id = 0, Tcp = pendingClient, Stream = pendingClient.GetStream() };
                pendingClient = null;
            }
            else if (Time.realtimeSinceStartup - connectStart > Config.ConnectTimeoutSeconds)
            {
                connectTask = null;
                Fail("Connection timed out");
            }
        }

        void Fail(string reason)
        {
            Debug.Log($"[TD] Net failure: {reason}");
            Dispose();
            Disconnected?.Invoke(reason);
        }

        void ReadPeer(Peer peer)
        {
            if (peer.Dead) return;

            try
            {
                while (peer.Stream.DataAvailable)
                {
                    int n = peer.Stream.Read(readTmp, 0, readTmp.Length);
                    if (n <= 0) { peer.Dead = true; break; }
                    Append(peer, n);
                }

                ParseFrames(peer);
                if (disposed) return;

                var socket = peer.Tcp.Client;
                if (!peer.Dead && socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    peer.Dead = true;
            }
            catch (Exception)
            {
                peer.Dead = true;
            }
        }

        void Append(Peer peer, int count)
        {
            if (peer.BufferLength + count > peer.Buffer.Length)
                Array.Resize(ref peer.Buffer, Math.Max(peer.Buffer.Length * 2, peer.BufferLength + count));
            Buffer.BlockCopy(readTmp, 0, peer.Buffer, peer.BufferLength, count);
            peer.BufferLength += count;
        }

        void ParseFrames(Peer peer)
        {
            int offset = 0;

            while (peer.BufferLength - offset >= 4)
            {
                int length = BitConverter.ToInt32(peer.Buffer, offset);
                if (length <= 0 || length > MaxFrameBytes) { peer.Dead = true; return; }
                if (peer.BufferLength - offset - 4 < length) break;

                var body = new byte[length];
                Buffer.BlockCopy(peer.Buffer, offset + 4, body, 0, length);
                offset += 4 + length;

                Dispatch(peer, body);
                if (disposed) return;
            }

            if (offset > 0)
            {
                int remaining = peer.BufferLength - offset;
                Buffer.BlockCopy(peer.Buffer, offset, peer.Buffer, 0, remaining);
                peer.BufferLength = remaining;
            }
        }

        void Dispatch(Peer from, byte[] body)
        {
            var type = (MsgType)body[0];

            try
            {
                using (var ms = new MemoryStream(body, 1, body.Length - 1))
                using (var reader = new BinaryReader(ms))
                {
                    if (IsHost)
                    {
                        ServerMessage?.Invoke(from.Id, type, reader);
                        return;
                    }

                    switch (type)
                    {
                        case MsgType.Welcome:
                            LocalId = reader.ReadInt32();
                            welcomed = true;
                            Debug.Log($"[TD] Welcome, id={LocalId}");
                            Connected?.Invoke();
                            break;
                        case MsgType.Rejected:
                            Fail(reader.ReadString());
                            break;
                        default:
                            ClientMessage?.Invoke(type, reader);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TD] Error handling {type}: {e}");
            }
        }

        void DrainLocal()
        {
            int guard = 256;
            while (!disposed && localQueue.Count > 0 && guard-- > 0)
            {
                var msg = localQueue.Dequeue();
                var type = (MsgType)msg.Body[0];

                try
                {
                    using (var ms = new MemoryStream(msg.Body, 1, msg.Body.Length - 1))
                    using (var reader = new BinaryReader(ms))
                    {
                        if (msg.ToServer) ServerMessage?.Invoke(LocalId, type, reader);
                        else ClientMessage?.Invoke(type, reader);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TD] Error handling local {type}: {e}");
                }
            }
        }

        void RemoveDead(List<Peer> snapshot)
        {
            foreach (var peer in snapshot)
            {
                if (!peer.Dead || disposed) continue;

                if (IsHost)
                {
                    if (!peers.Remove(peer.Id)) continue;
                    CloseTcp(peer.Tcp);
                    Debug.Log($"[TD] Peer {peer.Id} disconnected");
                    PeerDisconnected?.Invoke(peer.Id);
                }
                else
                {
                    var reason = welcomed ? "Lost connection to host" : "Could not connect to host";
                    Fail(reason);
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- utilitarios

        /// <summary>IPv4 locais (Wi-Fi/Ethernet) para mostrar ao host.</summary>
        public static List<string> LocalAddresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var text = ua.Address.ToString();
                        if (text.StartsWith("169.254.")) continue; // link-local
                        result.Add(text);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TD] Could not list local IPs: " + e.Message);
            }
            return result;
        }
    }
}
