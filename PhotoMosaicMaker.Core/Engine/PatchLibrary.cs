using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoMosaicMaker.Core.Engine
{
    public sealed class PatchLibrary : IDisposable
    {
        public IReadOnlyList<PatchRecord> Patches { get; }

        public PatchLibrary(IReadOnlyList<PatchRecord> patches)
        {
            Patches = patches;
        }

        public void Dispose()
        {
            foreach (var p in Patches)
            {
                p.Dispose();
            }
        }
    }
}
