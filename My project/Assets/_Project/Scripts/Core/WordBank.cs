using System;
using System.Text;

namespace TypeDuel
{
    /// <summary>
    /// Banco de palavras em ingles (sem acentos e sem pontuacao). Lista propria de palavras comuns.
    /// Para novos idiomas/modos, trocar por um TextPack (ScriptableObject) com a mesma interface.
    /// </summary>
    public static class WordBank
    {
        static readonly string[] Words =
        {
            "about", "above", "across", "act", "add", "after", "again", "against", "air", "all",
            "almost", "alone", "along", "always", "among", "animal", "answer", "any", "area", "around",
            "away", "baby", "back", "ball", "bank", "base", "beach", "bear", "beat", "become",
            "bed", "before", "begin", "behind", "believe", "below", "best", "better", "between", "big",
            "bird", "bit", "black", "blue", "boat", "body", "book", "both", "box", "boy",
            "bread", "bridge", "bright", "bring", "brother", "brown", "build", "burn", "busy", "buy",
            "call", "calm", "camp", "card", "care", "carry", "case", "catch", "cause", "center",
            "chair", "change", "check", "child", "choose", "circle", "city", "class", "clean", "clear",
            "climb", "clock", "close", "cloud", "coast", "cold", "color", "come", "cook", "cool",
            "corner", "count", "country", "course", "cover", "cross", "crowd", "cry", "cut", "dance",
            "dark", "day", "dead", "deal", "dear", "deep", "desert", "dinner", "dirt", "do",
            "dog", "door", "down", "draw", "dream", "dress", "drink", "drive", "drop", "dry",
            "duck", "during", "each", "ear", "early", "earth", "east", "easy", "eat", "edge",
            "egg", "end", "enemy", "enjoy", "enough", "enter", "equal", "even", "evening", "ever",
            "every", "exact", "example", "eye", "face", "fact", "fair", "fall", "family", "far",
            "farm", "fast", "father", "fear", "feel", "few", "field", "fight", "fill", "find",
            "fine", "finger", "fire", "first", "fish", "five", "flag", "flat", "float", "floor",
            "flower", "fly", "follow", "food", "foot", "force", "forest", "forget", "form", "found",
            "four", "free", "fresh", "friend", "front", "fruit", "full", "fun", "game", "garden",
            "gate", "gentle", "get", "giant", "gift", "girl", "give", "glad", "glass", "go",
            "gold", "good", "grass", "great", "green", "ground", "group", "grow", "guess", "gun",
            "hair", "half", "hall", "hand", "happy", "hard", "hat", "hate", "have", "head",
            "hear", "heart", "heavy", "help", "here", "hide", "high", "hill", "hold", "hole",
            "home", "hope", "horse", "hot", "hour", "house", "human", "hunt", "hurry", "ice",
            "idea", "inch", "inside", "iron", "island", "jump", "just", "keep", "key", "kick",
            "kind", "king", "kiss", "knee", "knife", "know", "lady", "lake", "land", "large",
            "last", "late", "laugh", "lead", "learn", "leave", "left", "leg", "less", "letter",
            "level", "life", "lift", "light", "line", "lion", "list", "listen", "little", "live",
            "long", "look", "lose", "loud", "love", "low", "luck", "lunch", "machine", "main",
            "make", "man", "many", "map", "mark", "market", "master", "may", "meal", "mean",
            "meet", "metal", "middle", "mile", "milk", "mind", "miss", "moment", "money", "month",
            "moon", "more", "morning", "most", "mother", "mountain", "mouse", "mouth", "move", "much",
            "music", "name", "narrow", "near", "neck", "need", "never", "new", "next", "nice",
            "night", "noise", "north", "nose", "note", "nothing", "notice", "now", "number", "object",
            "ocean", "off", "office", "often", "oil", "old", "once", "only", "open", "orange",
            "order", "other", "outside", "over", "own", "page", "pain", "paint", "pair", "paper",
            "park", "part", "party", "pass", "past", "path", "pay", "peace", "pen", "people",
            "pick", "picture", "piece", "pilot", "pink", "place", "plain", "plan", "plant", "play",
            "please", "pocket", "point", "pool", "poor", "power", "press", "pretty", "prince", "prize",
            "pull", "push", "queen", "quick", "quiet", "race", "rain", "raise", "reach", "read",
            "ready", "real", "red", "rest", "rich", "ride", "right", "ring", "rise", "river",
            "road", "rock", "roll", "roof", "room", "root", "rope", "rose", "round", "row",
            "run", "sad", "safe", "sail", "salt", "same", "sand", "save", "say", "school",
            "sea", "season", "seat", "second", "see", "seed", "sell", "send", "shade", "shake",
            "shape", "sharp", "sheep", "shine", "ship", "shirt", "shoe", "shoot", "shop", "short",
            "show", "side", "sign", "silver", "simple", "sing", "sister", "sit", "six", "size",
            "skin", "sky", "sleep", "slow", "small", "smell", "smile", "smoke", "snow", "soft",
            "soil", "some", "song", "soon", "sound", "south", "space", "speak", "speed", "spend",
            "spot", "spring", "square", "stand", "star", "start", "state", "stay", "steel", "step",
            "stick", "still", "stone", "stop", "store", "storm", "story", "street", "strong", "study",
            "such", "sudden", "sugar", "summer", "sun", "sweet", "swim", "table", "tail", "take",
            "talk", "tall", "taste", "teach", "team", "tell", "ten", "test", "thank", "thick",
            "thin", "thing", "think", "third", "thought", "three", "throw", "tie", "time", "tiny",
            "today", "tomorrow", "tonight", "tool", "tooth", "top", "touch", "tower", "town", "toy",
            "track", "trade", "train", "tree", "trip", "true", "try", "turn", "twice", "two",
            "under", "until", "up", "use", "usual", "very", "village", "visit", "voice", "wait",
            "walk", "wall", "want", "warm", "wash", "watch", "water", "wave", "way", "weak",
            "wear", "weather", "week", "weight", "well", "west", "wet", "wheel", "when", "where",
            "white", "whole", "wide", "wife", "wild", "will", "win", "wind", "window", "winter",
            "wire", "wise", "wish", "wolf", "woman", "wood", "word", "work", "world", "worry",
            "write", "wrong", "yard", "year", "yellow", "yes", "young", "zero", "zone",
        };

        /// <summary>Sorteia `count` palavras diferentes e embaralhadas, separadas por um espaco.</summary>
        public static string GenerateText(Random rng, int count)
        {
            count = Math.Min(count, Words.Length);

            var pool = (string[])Words.Clone();
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(pool[i]);
            }
            return sb.ToString();
        }
    }
}
