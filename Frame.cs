using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace BTL_DLCN
{
    public class Frame
    {
        public byte Start { get; set; } = 0xAA;
        public byte Command { get; set; }
        public ushort Length { get; set; }
        public byte[] Payload { get; set; }
        public ushort CRC_16 { get; set; }
        public byte End { get; set; } = 0xAF;

        
    }
        
}
