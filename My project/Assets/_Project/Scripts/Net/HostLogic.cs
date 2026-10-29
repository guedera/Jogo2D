using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TypeDuel.Net
{
    /// <summary>
    /// Autoridade da partida (roda so no host): texto, sorteio de poderes, roteamento dos poderes e vencedor.
    /// Fala em "outros jogadores", nunca em "o adversario", para escalar alem de 2 jogadores.
    /// </summary>
    public sealed class HostLogic
    {
        enum State { Lobby, Playing, Over }

        readonly GameNet net;
        readonly System.Random rng = new System.Random();
        readonly List<int> players = new List<int>();
        readonly Dictionary<int, HashSet<int>> countedWords = new Dictionary<int, HashSet<int>>();

        State state = State.Lobby;
        int wordCount;

        public int PlayerCount => players.Count;
        public bool CanStart => state != State.Playing && players.Count >= Config.MinPlayersToStart;

        public HostLogic(GameNet net)
        {
            this.net = net;
            players.Add(net.LocalId);

            net.CanAccept = () => state != State.Playing && players.Count < Config.MaxPlayers;
            net.PeerConnected += OnPeerConnected;
            net.PeerDisconnected += OnPeerDisconnected;
            net.ServerMessage += OnMessage;
        }

        public void SendLobbyState()
        {
            net.Broadcast(MsgType.Lobby, w =>
            {
                w.Write(players.Count);
                foreach (var id in players) w.Write(id);
            });
        }

        public void StartMatch()
        {
            if (!CanStart) return;

            var text = WordBank.GenerateText(rng, Config.WordCount);
            wordCount = Config.WordCount;
            state = State.Playing;
            countedWords.Clear();
            foreach (var id in players) countedWords[id] = new HashSet<int>();

            Debug.Log($"[TD] Match start, {players.Count} players, {text.Length} chars");
            net.Broadcast(MsgType.Start, w =>
            {
                w.Write(text);
                w.Write(players.Count);
                foreach (var id in players) w.Write(id);
            });
        }

        void OnPeerConnected(int id)
        {
            players.Add(id);
            SendLobbyState();
        }

        void OnPeerDisconnected(int id)
        {
            players.Remove(id);
            countedWords.Remove(id);

            if (state == State.Playing && players.Count < 2)
            {
                state = State.Over;
                int winner = players.Count > 0 ? players[0] : -1;
                net.Broadcast(MsgType.GameOver, w => { w.Write(winner); w.Write((byte)1); });
            }
            else if (state != State.Playing)
            {
                state = State.Lobby;
            }

            SendLobbyState();
        }

        void OnMessage(int sender, MsgType type, BinaryReader r)
        {
            if (!players.Contains(sender)) return;

            switch (type)
            {
                case MsgType.WordDone: OnWordDone(sender, r.ReadInt32()); break;
                case MsgType.Progress: OnProgress(sender, r.ReadInt32()); break;
                case MsgType.UsePower: OnUsePower(sender, r.ReadByte()); break;
                case MsgType.Finished: OnFinished(sender); break;
            }
        }

        void OnWordDone(int sender, int wordIndex)
        {
            if (state != State.Playing) return;
            if (wordIndex < 0 || wordIndex >= wordCount) return;
            if (!countedWords.TryGetValue(sender, out var counted) || !counted.Add(wordIndex)) return;

            if (rng.NextDouble() >= Config.PowerChance) return;

            var power = (byte)rng.Next(PowerInfo.Count);
            net.SendToClient(sender, MsgType.PowerGranted, w => w.Write(power));
        }

        void OnProgress(int sender, int prefix)
        {
            if (state != State.Playing) return;

            foreach (var id in players)
            {
                if (id == sender) continue;
                net.SendToClient(id, MsgType.OpponentProgress, w => { w.Write(sender); w.Write(prefix); });
            }
        }

        void OnUsePower(int sender, byte power)
        {
            if (state != State.Playing || power >= PowerInfo.Count) return;

            // Alvos: todos os outros jogadores (no MVP, so existe um).
            foreach (var id in players)
            {
                if (id == sender) continue;
                net.SendToClient(id, MsgType.PowerEffect, w => { w.Write(power); w.Write(sender); });
            }
        }

        void OnFinished(int sender)
        {
            if (state != State.Playing) return;

            state = State.Over;
            Debug.Log($"[TD] Winner: {sender}");
            net.Broadcast(MsgType.GameOver, w => { w.Write(sender); w.Write((byte)0); });
        }
    }
}
