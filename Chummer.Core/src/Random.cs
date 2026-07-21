//Using

using System;
using System.Security.Cryptography;

namespace Chummer.Core
{
    public class Mtrng
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MatrixA = 0x9908b0df;
        private const uint UpperMask = 0x80000000;
        private const uint LowerMask = 0x7fffffff;

        private const uint TemperingMaskB = 0x9d2c5680;
        private const uint TemperingMaskC = 0xefc60000;

        private static readonly uint[] Mag01 = { 0x0, MatrixA };

        private readonly uint[] _mt = new uint[N];

        private short _mti;

        public uint UsedSeed;

        public Mtrng()
        {
            var rngcsp = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rngcsp.GetBytes(bytes);
            UsedSeed = BitConverter.ToUInt32(bytes, 0);
            _mt[0] = UsedSeed & 0xffffffffU;
            for (_mti = 1; _mti < N; ++_mti) _mt[_mti] = (69069 * _mt[_mti - 1]) & 0xffffffffU;
        }

        public Mtrng(uint seed)
        {
            if (seed == 0)
            {
                var rngcsp = RandomNumberGenerator.Create();
                var bytes = new byte[4];
                rngcsp.GetBytes(bytes);
                UsedSeed = BitConverter.ToUInt32(bytes, 0);
            }

            UsedSeed = seed;
            _mt[0] = UsedSeed & 0xffffffffU;
            for (_mti = 1; _mti < N; ++_mti) _mt[_mti] = (69069 * _mt[_mti - 1]) & 0xffffffffU;
        }

        private static uint TEMPERING_SHIFT_U(uint y)
        {
            return y >> 11;
        }

        private static uint TEMPERING_SHIFT_S(uint y)
        {
            return y << 7;
        }

        private static uint TEMPERING_SHIFT_T(uint y)
        {
            return y << 15;
        }

        private static uint TEMPERING_SHIFT_L(uint y)
        {
            return y >> 18;
        }

        protected uint GenerateUInt()
        {
            uint y;

            if (_mti >= N)
            {
                short kk = 0;

                for (; kk < N - M; ++kk)
                {
                    y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                    _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ Mag01[y & 0x1];
                }

                for (; kk < N - 1; ++kk)
                {
                    y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                    _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ Mag01[y & 0x1];
                }

                y = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
                _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ Mag01[y & 0x1];

                _mti = 0;
            }

            y = _mt[_mti++];
            y ^= TEMPERING_SHIFT_U(y);
            y ^= TEMPERING_SHIFT_S(y) & TemperingMaskB;
            y ^= TEMPERING_SHIFT_T(y) & TemperingMaskC;
            y ^= TEMPERING_SHIFT_L(y);

            return y;
        }

        public virtual uint NextUInt()
        {
            return GenerateUInt();
        }

        public virtual uint NextUInt(uint maxValue)
        {
            return (uint)(GenerateUInt() / ((double)uint.MaxValue / maxValue));
        }

        public virtual uint NextUInt(uint minValue, uint maxValue)
        {
            if (minValue >= maxValue) throw new ArgumentOutOfRangeException();

            return (uint)(GenerateUInt() / ((double)uint.MaxValue / (maxValue - minValue)) + minValue);
        }

        public int Next()
        {
            return Next(int.MaxValue);
        }

        public int Next(int maxValue)
        {
            if (maxValue <= 1)
            {
                if (maxValue < 0) throw new ArgumentOutOfRangeException();

                return 0;
            }

            return (int)(NextDouble() * maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (maxValue < minValue) throw new ArgumentOutOfRangeException();

            if (maxValue == minValue) return minValue;

            return Next(maxValue - minValue) + minValue;
        }

        public void NextBytes(byte[] buffer)
        {
            var bufLen = buffer.Length;

            if (buffer == null) throw new ArgumentNullException();

            for (var idx = 0; idx < bufLen; ++idx) buffer[idx] = (byte)Next(256);
        }

        public double NextDouble()
        {
            return (double)GenerateUInt() / ((ulong)uint.MaxValue + 1);
        }
    }
}