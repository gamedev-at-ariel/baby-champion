using System;
using System.Globalization;

namespace BabyChampion.Game
{
    /// <summary>
    /// Runtime arguments accepted by development builds:
    /// <c>-level N -timescale X -seed S -skipIntro</c> on the command line (Android passes
    /// them through the <c>unity</c> intent extra), or <c>?level=N&amp;timescale=X</c> on Web.
    /// </summary>
    public sealed class DevArgs
    {
        public int? Level { get; private set; }
        public float? TimeScale { get; private set; }
        public int? Seed { get; private set; }
        public bool SkipIntro { get; private set; }

        public static DevArgs FromCommandLine(string[] args)
        {
            var result = new DevArgs();
            if (args == null) return result;

            for (int i = 0; i < args.Length; i++)
            {
                string name = args[i].TrimStart('-').ToLowerInvariant();
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
                if (result.Apply(name, value) && value != null) i++;
            }
            return result;
        }

        public static DevArgs FromUrl(string url)
        {
            var result = new DevArgs();
            if (string.IsNullOrEmpty(url)) return result;

            int q = url.IndexOf('?');
            if (q < 0) return result;
            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);

            foreach (string pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                string name = Uri.UnescapeDataString(eq < 0 ? pair : pair.Substring(0, eq)).ToLowerInvariant();
                string value = eq < 0 ? null : Uri.UnescapeDataString(pair.Substring(eq + 1));
                result.Apply(name, value);
            }
            return result;
        }

        // Returns true when the argument consumed a value.
        bool Apply(string name, string value)
        {
            switch (name)
            {
                case "level":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)) Level = level;
                    return true;
                case "timescale":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) && scale > 0f) TimeScale = scale;
                    return true;
                case "seed":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)) Seed = seed;
                    return true;
                case "skipintro":
                    SkipIntro = value == null || value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    return value != null;
                default:
                    return false;
            }
        }
    }
}
