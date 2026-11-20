using System;
using System.Collections.Generic;
using System.IO;

namespace Vampire
{
    // One open workbook must not hold up the other end-of-round reports.
    internal sealed class GStarReportBatch
    {
        readonly Action<string, string> writeFile;
        readonly List<string> failed = new List<string>();

        public GStarReportBatch(Action<string, string> writeFile) { this.writeFile = writeFile; }
        public bool Succeeded => failed.Count == 0;
        public string FailedNames => string.Join(", ", failed);

        public void Write(string name, string contents)
        {
            try { writeFile(name, contents); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { failed.Add(name); }
        }
    }
}
