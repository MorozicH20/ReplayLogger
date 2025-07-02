using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegitimateChallenge
{
    internal class ModVersion
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Hash { get { return ModsChecking.CalculateSHA256(Path); }}
        public string Path { get; set; }
    }
}
