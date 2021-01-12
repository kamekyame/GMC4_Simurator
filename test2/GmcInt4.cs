using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gmc4
{
    public struct Int4 : IFormattable
    {
        int m_value;

        Int4(int v)
        {
            m_value = v & 0xF;
        }

        public override String ToString()
        {
            return m_value.ToString();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return m_value.ToString(format, formatProvider);
        }

        public static implicit operator Int4(int v)
        {
            return new Int4(v);
        }
        public static implicit operator int(Int4 v)
        {
            return v.m_value;
        }
    }
}
