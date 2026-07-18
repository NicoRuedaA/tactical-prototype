using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Independent deterministic random streams derived from a run seed.
    /// Adding random draws to one stream cannot perturb another stream.
    /// </summary>
    public enum RunRandomStream
    {
        RewardOptions = 1,
        RewardRecipient = 2,
        Encounters = 3,
    }

    public static class RunSeedStreams
    {
        /// <summary>
        /// Derives a stable seed from the run seed, stream domain, and progress index.
        /// Uses fixed integer mixing rather than runtime-dependent string hashing.
        /// </summary>
        public static int Derive(int runSeed, RunRandomStream stream, int progressIndex)
        {
            unchecked
            {
                uint value = (uint)runSeed;
                value ^= (uint)stream * 0x9E3779B9u;
                value = Mix(value);
                value ^= Mix((uint)progressIndex + 0x85EBCA6Bu);
                return (int)Mix(value);
            }
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }
    }

    /// <summary>
    /// Small platform-stable PRNG for deterministic gameplay selections.
    /// </summary>
    public struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            _state = unchecked((uint)seed);
            if (_state == 0)
                _state = 0x6D2B79F5u;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            uint value = NextUInt();
            return (int)(value % (uint)maxExclusive);
        }

        public IReadOnlyList<int> PickDistinctIndices(int populationSize, int count)
        {
            if (populationSize < 0)
                throw new ArgumentOutOfRangeException(nameof(populationSize));
            if (count < 0 || count > populationSize)
                throw new ArgumentOutOfRangeException(nameof(count));

            var available = new List<int>(populationSize);
            for (int i = 0; i < populationSize; i++)
                available.Add(i);

            var selected = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int index = Next(available.Count);
                selected.Add(available[index]);
                available.RemoveAt(index);
            }

            return selected;
        }

        private uint NextUInt()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
