namespace BabyChampion.Simulation
{
    /// <summary>
    /// Deterministic pseudo-random generator (xorshift32). The simulation never uses
    /// System.Random or UnityEngine.Random, so a seed always reproduces the same run.
    /// </summary>
    public sealed class SeededRandom
    {
        uint _state;

        public SeededRandom(int seed)
        {
            // xorshift must never hold a zero state.
            _state = (uint)seed;
            if (_state == 0) _state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform value in [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0;

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }
    }
}
