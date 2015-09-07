//Using
using System;
using System.Security.Cryptography;

namespace Chummer
{
    class MTRNG
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 0x9908b0df;
        private const uint UPPER_MASK = 0x80000000;
        private const uint LOWER_MASK = 0x7fffffff;

        private const uint TEMPERING_MASK_B = 0x9d2c5680;
        private const uint TEMPERING_MASK_C = 0xefc60000;

        private static uint TEMPERING_SHIFT_U(uint y) { return (y >> 11); }
        private static uint TEMPERING_SHIFT_S(uint y) { return (y << 7); }
        private static uint TEMPERING_SHIFT_T(uint y) { return (y << 15); }
        private static uint TEMPERING_SHIFT_L(uint y) { return (y >> 18); }

        private uint[] mt = new uint[N];

        private short mti;

        private static uint[] mag01 = { 0x0, MATRIX_A };

        public uint usedSeed;

        public MTRNG()
        {
            RNGCryptoServiceProvider rngcsp = new RNGCryptoServiceProvider();
            byte[] bytes = new byte[4];
            rngcsp.GetBytes(bytes);
            usedSeed = BitConverter.ToUInt32(bytes,0);
            mt[0] = usedSeed & 0xffffffffU;
            for (mti = 1; mti < N; ++mti)
            {
                mt[mti] = (69069 * mt[mti - 1]) & 0xffffffffU;
            }
        }

        public MTRNG(uint seed)
		{
            if (seed == 0)
            {
                RNGCryptoServiceProvider rngcsp = new RNGCryptoServiceProvider();
                byte[] bytes = new byte[4];
                rngcsp.GetBytes(bytes);
                usedSeed = BitConverter.ToUInt32(bytes, 0);
            }
            usedSeed = seed;
            mt[0] = usedSeed & 0xffffffffU;
			for (mti = 1; mti < N; ++mti)
			{
				mt[mti] = (69069 * mt[mti - 1]) & 0xffffffffU;
			}
		}

        protected uint GenerateUInt()
        {
            uint y;

            if (mti >= N)
            {
                short kk = 0;

                for (; kk < N - M; ++kk)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1];
                }

                for (; kk < N - 1; ++kk)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1];
                }

                y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1];

                mti = 0;
            }

            y = mt[mti++];
            y ^= TEMPERING_SHIFT_U(y);
            y ^= TEMPERING_SHIFT_S(y) & TEMPERING_MASK_B;
            y ^= TEMPERING_SHIFT_T(y) & TEMPERING_MASK_C;
            y ^= TEMPERING_SHIFT_L(y);

            return y;
        }

        public virtual uint NextUInt()
        {
            return this.GenerateUInt();
        }

        public virtual uint NextUInt(uint maxValue)
        {
            return (uint)(this.GenerateUInt() / ((double)uint.MaxValue / maxValue));
        }

        public virtual uint NextUInt(uint minValue, uint maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            return (uint)(this.GenerateUInt() / ((double)uint.MaxValue / (maxValue - minValue)) + minValue);
        }

        public int Next()
        {
            return this.Next(int.MaxValue);
        }

        public int Next(int maxValue)
        {
            if (maxValue <= 1)
            {
                if (maxValue < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                return 0;
            }

            return (int)(this.NextDouble() * maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (maxValue < minValue)
            {
                throw new ArgumentOutOfRangeException();
            }
            else if (maxValue == minValue)
            {
                return minValue;
            }
            else
            {
                return this.Next(maxValue - minValue) + minValue;
            }
        }

        public void NextBytes(byte[] buffer)
        {
            int bufLen = buffer.Length;

            if (buffer == null)
            {
                throw new ArgumentNullException();
            }

            for (int idx = 0; idx < bufLen; ++idx)
            {
                buffer[idx] = (byte)this.Next(256);
            }
        }

        public double NextDouble()
        {
            return (double)this.GenerateUInt() / ((ulong)uint.MaxValue + 1);
        }
    }
}
