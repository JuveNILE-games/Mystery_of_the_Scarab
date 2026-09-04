using System.Collections.Generic;

namespace Game.Systems.LevelGeneration.Editor
{
    // Self-contained deterministic PRNG (xoshiro128**) — pure integer arithmetic,
    // stable across platforms, OSes, and Unity/.NET versions for the life of the
    // project. Never use UnityEngine.Random or System.Random for generation:
    // neither is guaranteed stable across runtimes (SCARAB-002).
    public sealed class DeterministicRandom
    {
        private uint _s0, _s1, _s2, _s3;

        public DeterministicRandom(uint seed)
        {
            unchecked
            {
                uint state = seed;
                _s0 = SplitMix32(ref state);
                _s1 = SplitMix32(ref state);
                _s2 = SplitMix32(ref state);
                _s3 = SplitMix32(ref state);
            }

            if ((_s0 | _s1 | _s2 | _s3) == 0)
                _s0 = 1; // xoshiro requires a non-zero state
        }

        private static uint SplitMix32(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x21F0AAADu;
                z = (z ^ (z >> 15)) * 0x735A2D97u;
                return z ^ (z >> 15);
            }
        }

        private static uint RotL(uint x, int k) => (x << k) | (x >> (32 - k));

        public uint NextUInt32()
        {
            unchecked
            {
                uint result = RotL(_s1 * 5u, 7) * 9u;

                uint t = _s1 << 9;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = RotL(_s3, 11);

                return result;
            }
        }

        // Deterministic double in [0, 1).
        public double NextDouble() => NextUInt32() / 4294967296.0;

        // Deterministic int in [minInclusive, maxExclusive).
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt32() % range);
        }

        public bool NextBool(double trueChance = 0.5) => NextDouble() < trueChance;

        public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(0, items.Count)];

        // Fisher-Yates, deterministic, in place.
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        // Derives a named, deterministic sub-stream from a master seed (SCARAB-002) —
        // e.g. ForStage(seed, "placement") and ForStage(seed, "connectivity") never
        // collide and always reproduce the same sequence for the same stage name.
        public static DeterministicRandom ForStage(int masterSeed, string stageName)
        {
            unchecked
            {
                uint combined = ((uint)masterSeed * 0x01000193u) ^ Fnv1aHash(stageName);
                return new DeterministicRandom(combined);
            }
        }

        private static uint Fnv1aHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
