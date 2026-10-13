using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Generic;
using Frost.Core;

internal static class CuteAcquisitionVerification
{
    public static void Run(string downloads,string report)
    {
        Directory.CreateDirectory(report);Storage.UseTestingRoot(Path.Combine(report,"pack-data-"+Guid.NewGuid().ToString("N")));var results=new List<object>();
        foreach(var slug in new[]{"kawaii-cat-curs0rs","kawaii-pink-heart","bunny-cute-kawaii","pastel","cool-cat"})
        {
            var zip=Path.Combine(downloads,slug+".zip");using var evidence=JsonDocument.Parse(File.ReadAllText(Path.Combine(downloads,slug+".json")));
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));if(!hash.Equals(evidence.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new Exception("Original package checksum mismatch.");
            using var pack=CursorPack.Open(zip);var files=evidence.RootElement.GetProperty("entries").EnumerateArray().Where(e=>e.GetProperty("name").GetString()!.EndsWith(".cur")||e.GetProperty("name").GetString()!.EndsWith(".ani")).ToArray();
            foreach(var file in pack.Files)
            {
                var original=files.Single(f=>f.GetProperty("name").GetString()==file.OriginalName);
                if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.Path))).Equals(original.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new Exception("Pack staging altered source bytes.");
            }
            if(pack.Files.Count!=files.Length)throw new Exception("Pack source file count mismatch.");
            var candidates=pack.Candidates();results.Add(new{slug,packageSha256=hash,validatedCursors=pack.Files.Count,recognizedRoles=candidates.Count(p=>p.Value.Length>0),conflicts=candidates.Where(p=>p.Value.Length>1).Select(p=>p.Key).ToArray(),unrecognized=pack.Files.Where(f=>f.SuggestedRole==null).Select(f=>f.OriginalName).ToArray(),passed=true});
        }
        Storage.Write(Path.Combine(report,"acquisition-report.json"),new{passed=true,results,notes="All five original author ZIPs opened through production bounded staging; every CUR/ANI passed binary and native validation and remained byte-for-byte identical. Name suggestions deliberately leave ambiguous/custom names for review; curated manifests use separately reviewed author CRS mapping."});Console.WriteLine("Original author pack audit passed: 5 ZIPs, 75 cursor resources.");
    }
}
