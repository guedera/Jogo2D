using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        sealed class Suggestion
        {
            public string Title;
            public string Text;
            public int Words;
        }

        /// <summary>Nada escolhido ainda: o host precisa escolher antes de comecar.</summary>
        public const int NoChoice = -2;
        /// <summary>Texto gerado com o banco de palavras (modo original).</summary>
        public const int GenerateChoice = -1;

        readonly GameNet net;
        readonly System.Random rng = new System.Random();
        readonly List<int> players = new List<int>();
        readonly Dictionary<int, HashSet<int>> countedWords = new Dictionary<int, HashSet<int>>();
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        readonly Dictionary<int, Suggestion> suggestions = new Dictionary<int, Suggestion>();
        readonly Dictionary<int, int> progress = new Dictionary<int, int>();
        readonly Dictionary<int, Queue<byte>> inventories = new Dictionary<int, Queue<byte>>();
        readonly HashSet<int> powerWords = new HashSet<int>();
        IReadOnlyList<int> wordEnds;
        int textLength;

        State state = State.Lobby;
        int wordCount;
        int choice = NoChoice;

        public int PlayerCount => players.Count;
        public bool CanStart => state != State.Playing && players.Count >= Config.MinPlayersToStart && choice != NoChoice;

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

        /// <summary>Host escolhe o texto gerado (banco de palavras).</summary>
        public void ChooseGenerate()
        {
            if (choice == GenerateChoice) return;
            choice = GenerateChoice;
            SendSuggestions();
        }

        /// <summary>Host escolhe o texto sugerido por um jogador (id do dono).</summary>
        public void ChooseSuggestion(int ownerId)
        {
            if (!suggestions.ContainsKey(ownerId)) return;
            choice = ownerId;
            SendSuggestions();
        }

        /// <summary>Envia a todos a lista de textos sugeridos e o que o host escolheu.</summary>
        public void SendSuggestions()
        {
            net.Broadcast(MsgType.Suggestions, w =>
            {
                w.Write(choice);
                w.Write(suggestions.Count);
                foreach (var pair in suggestions)
                {
                    w.Write(pair.Key);
                    w.Write(names.TryGetValue(pair.Key, out var owner) ? owner : "");
                    w.Write(pair.Value.Title);
                    w.Write(pair.Value.Words);
                }
            });
        }

        /// <summary>Revanche: todos voltam para a sala de espera, com o texto escolhido antes ja marcado.</summary>
        public void ReturnToLobby()
        {
            if (state == State.Playing) return;

            state = State.Lobby;
            net.Broadcast(MsgType.ReturnToLobby);
            SendLobbyState();
            SendSuggestions();
        }

        public void StartMatch()
        {
            if (!CanStart) return;

            var text = choice >= 0 && suggestions.TryGetValue(choice, out var picked)
                ? picked.Text
                : WordBank.GenerateText(rng, Config.WordCount);
            wordCount = text.Split(' ').Length;
            state = State.Playing;
            countedWords.Clear();
            progress.Clear();
            inventories.Clear();
            wordEnds = new TypingModel(text, 0).WordEnds;
            powerWords.Clear();
            var candidates = new List<int>();
            // A ultima palavra encerra a partida, portanto nao concede poder.
            for (int i = 0; i < wordCount - 1; i++) candidates.Add(i);
            int rewardCount = Math.Min(candidates.Count, Math.Max(1, (int)Math.Round(wordCount * Config.PowerWordFraction)));
            for (int i = 0; i < rewardCount; i++)
            {
                int pick = rng.Next(candidates.Count);
                powerWords.Add(candidates[pick]);
                candidates.RemoveAt(pick);
            }
            foreach (var id in players)
            {
                countedWords[id] = new HashSet<int>();
                progress[id] = 0;
                inventories[id] = new Queue<byte>();
            }
            textLength = text.Length;

            Debug.Log($"[TD] Match start, {players.Count} players, {text.Length} chars");
            net.Broadcast(MsgType.Start, w =>
            {
                w.Write(text);
                w.Write(players.Count);
                foreach (var id in players) w.Write(id);
                foreach (var id in players) w.Write(names.TryGetValue(id, out var name) ? name : "");
                w.Write(powerWords.Count);
                foreach (int index in powerWords) w.Write(index);
            });
        }

        void OnPeerConnected(int id)
        {
            players.Add(id);
            SendLobbyState();
            SendSuggestions();
        }

        void OnPeerDisconnected(int id)
        {
            players.Remove(id);
            countedWords.Remove(id);
            names.Remove(id);
            progress.Remove(id);
            inventories.Remove(id);
            suggestions.Remove(id);
            if (choice == id) choice = NoChoice;

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
            SendSuggestions();
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
                case MsgType.SetName: names[sender] = CleanName(r.ReadString()); break;
                case MsgType.SuggestText: OnSuggestText(sender, r.ReadString(), r.ReadString()); break;
            }
        }

        /// <summary>Texto sugerido por um jogador: o host limpa e valida de novo (nao confia no cliente). O texto do proprio host ja fica escolhido.</summary>
        void OnSuggestText(int sender, string title, string raw)
        {
            if (state == State.Playing) return;
            if (!TextCleaner.TryClean(raw, out var cleaned, out int words)) return;

            if (title.Length > 24) title = title.Substring(0, 24);
            suggestions[sender] = new Suggestion { Title = title, Text = cleaned, Words = words };
            if (sender == net.LocalId) choice = sender;
            SendSuggestions();
        }

        /// <summary>Nome vindo de um cliente: so ASCII visivel (sem &lt; e &gt;, que quebram o rich text), limitado ao tamanho maximo.</summary>
        static string CleanName(string raw)
        {
            var sb = new StringBuilder();
            foreach (char c in raw)
            {
                if (sb.Length >= Config.MaxNameLength) break;
                if (c >= ' ' && c <= '~' && c != '<' && c != '>') sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        void OnWordDone(int sender, int wordIndex)
        {
            if (state != State.Playing) return;
            if (wordIndex < 0 || wordIndex >= wordCount) return;
            if (!countedWords.TryGetValue(sender, out var counted) || !counted.Add(wordIndex)) return;

            if (!powerWords.Contains(wordIndex) || progress[sender] < wordEnds[wordIndex]) return;
            var inventory = inventories[sender];
            if (inventory.Count >= Config.MaxHeldPowers) return;

            // Empates nao habilitam AUTOCOMPLETE. A elegibilidade e decidida ao receber.
            bool behind = false;
            foreach (var pair in progress)
                if (pair.Key != sender && pair.Value > progress[sender]) behind = true;
            var power = (byte)rng.Next(behind ? PowerInfo.Count : PowerInfo.Count - 1);
            inventory.Enqueue(power);
            net.SendToClient(sender, MsgType.PowerGranted, w => w.Write(power));
        }

        void OnProgress(int sender, int prefix)
        {
            if (state != State.Playing) return;

            prefix = Mathf.Clamp(prefix, 0, textLength);
            progress[sender] = prefix;

            foreach (var id in players)
            {
                if (id == sender) continue;
                net.SendToClient(id, MsgType.OpponentProgress, w => { w.Write(sender); w.Write(prefix); });
            }
        }

        void OnUsePower(int sender, byte power)
        {
            if (state != State.Playing || power >= PowerInfo.Count) return;

            if (!inventories.TryGetValue(sender, out var inventory) || inventory.Count == 0 || inventory.Peek() != power) return;
            inventory.Dequeue();

            if ((PowerType)power == PowerType.Autocomplete)
            {
                net.SendToClient(sender, MsgType.PowerEffect, w => { w.Write(power); w.Write(sender); });
                return;
            }

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
