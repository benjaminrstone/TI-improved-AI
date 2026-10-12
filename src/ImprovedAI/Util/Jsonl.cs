using System;
using System.IO;
using Newtonsoft.Json;

namespace ImprovedAI.Util
{
    /// <summary>Append-only JSON-lines file; flushed per line so a killed game still leaves usable data.</summary>
    internal sealed class Jsonl : IDisposable
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        private readonly StreamWriter _w;
        public readonly string Path;

        public Jsonl(string path)
        {
            Path = path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            _w = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }

        public void Write(object record)
        {
            try { _w.WriteLine(JsonConvert.SerializeObject(record, Settings)); }
            catch (Exception e) { Plugin.Log.LogWarning($"[Jsonl] {Path}: {e.Message}"); }
        }

        public void Dispose() => _w.Dispose();

        public static string Serialize(object o, bool indented = false) =>
            JsonConvert.SerializeObject(o, indented ? Formatting.Indented : Formatting.None, Settings);
    }
}
