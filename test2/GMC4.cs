using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gmc4
{
    enum RegName
    {
        A_REG = 0x6F, B_REG = 0x6C, Y_REG = 0x6E, Z_REG = 0x6D,
        A_REGD = 0x69, B_REGD = 0x67, Y_REGD = 0x68, Z_REGD = 0x66,
    }

    class GMC4
    {
        private Int4[] mem = new Int4[0x70];
        public Int4[] Mem { get { return mem; } }

        public GMC4()
        {
            MemReset();
        }


        void MemReset()
        {
            for (int i = 0; i < mem.Length; i++)
            {
                mem[i] = 0xF;
            }
        }

        public bool MemSet(int adr, Int4 v) { mem[CA(adr)] = v; return true; }
        public bool MemSet(int adr, char v) { mem[CA(adr)] = CharToInt(v); return true; }
        public bool MemSet(RegName n, Int4 v) => MemSet((byte)n, v);//mem[(byte)n] = v;
        public bool MemSet(RegName n, char v) => MemSet((byte)n, v);// mem[(byte)n] = CharToInt(v);
        public Int4 MemGet(int adr) => mem[CA(adr)];
        private Int4 MemGet(RegName n) => mem[(byte)n];

        public bool DataSet(Int4 v) => MemSet(CA(0x50 + MemGet(RegName.Y_REG)), v);
        public bool DataSet(char v) => MemSet(CA(0x50 + MemGet(RegName.Y_REG)), CharToInt(v));
        public bool DataSet(Int4 offset, Int4 v) => MemSet(CA(0x50 + offset), v);
        public bool DataSet(Int4 offset, char v) => MemSet(CA(0x50 + offset), CharToInt(v));
        public Int4 DataGet() => MemGet(CA(0x50 + MemGet(RegName.Y_REG)));
        public Int4 DataGet(Int4 offset) => MemGet(CA(0x50 + offset));

        public bool AregSet(int v) => MemSet((int)RegName.A_REG, v);
        public bool AregSet(char v) => MemSet((int)RegName.A_REG, CharToInt(v));
        public Int4 AregGet() => MemGet(RegName.A_REG);

        public bool YregSet(int v) => MemSet((int)RegName.Y_REG, v);
        public bool YregSet(char v) => MemSet((int)RegName.Y_REG, CharToInt(v));
        public Int4 YregGet() => MemGet(RegName.Y_REG);

        public bool SwapReg(RegName a, RegName b)
        {
            Int4 m = MemGet(a);
            MemSet(a, MemGet(b));
            MemSet(b, m);
            return true;
        }

        private int CharToInt(char a)
        {
            if (a >= '0' && a <= '9')
                return a - '0';
            else if (a >= 'A' && a <= 'F')
                return a - 'A' + 10;
            else throw new GMC4Exception("オペランドの値が不正です。");
            //return -1;
        }

        public static byte CA(int adr) => CA((byte)adr);
        public static byte CA(byte adr) // CheckAddress
        {
            if ((adr >= 0x60 && adr <= 0x65) || adr == 0x6A || adr == 0x6B || adr > 0x6F)
                throw new GMC4Exception($"指定したアドレス(0x{adr:X2})は無効です。");
            else return adr;
        }
    }

    class GMC4Exception : Exception
    {
        public GMC4Exception()
        : base()
        {
        }

        public GMC4Exception(string message)
            : base(message)
        {
        }

        public GMC4Exception(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
