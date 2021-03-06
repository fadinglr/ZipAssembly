// Copyright (c) 2018-2020, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Load assemblies from a zip file.
    /// </summary>
    [Serializable]
    public sealed class ZipAssembly : Assembly
    {
        // always set to Zip file full path + \\ + file path in zip.
        private string locationValue;

        // hopefully this has the path to the assembly on System.Reflection.Assembly.Location output with the value from this override.

        /// <summary>
        /// Gets the location of the assembly in the zip file.
        /// </summary>
        public override string Location => this.locationValue;

        /// <summary>
        /// Loads the assembly with it’s debugging symbols
        /// from the specified zip file.
        /// </summary>
        /// <param name="zipFileName">The zip file for which to look for the assembly in.</param>
        /// <param name="assemblyName">The assembly file name to load.</param>
        /// <param name="loadPDBFile">Loads the assemblies debugging symbols (pdb file) if true.</param>
        /// <returns>A new <see cref="ZipAssembly"/> that represents the loaded assembly.</returns>
        // TODO: Document the possible exceptions thrown directly from this method.
        public static ZipAssembly LoadFromZip(string zipFileName, string assemblyName, bool loadPDBFile = false)
        {
            if (string.IsNullOrWhiteSpace(zipFileName))
            {
                throw new ArgumentException($"{nameof(zipFileName)} is not allowed to be empty.", nameof(zipFileName));
            }
            else if (!File.Exists(zipFileName))
            {
                throw new ArgumentException($"{nameof(zipFileName)} does not exist.", nameof(zipFileName));
            }

            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                throw new ArgumentException($"{nameof(assemblyName)} is not allowed to be empty.", nameof(assemblyName));
            }
            else if (!assemblyName.EndsWith(".dll", StringComparison.Ordinal))
            {
                // setting pdbFileName fails or makes unpredicted/unwanted things if this is not checked
                throw new ArgumentException($"{nameof(assemblyName)} must end with '.dll' to be a valid assembly name.", nameof(assemblyName));
            }

            // check if the assembly is in the zip file.
            // If it is, get it’s bytes then load it.
            // If not throw an exception. Also throw
            // an exception if the pdb file is requested but not found.
            var found = false;
            var pdbfound = false;
            var zipAssemblyName = string.Empty;
            byte[] asmbytes = null;
            byte[] pdbbytes = null;
            using (var zipFile = ZipFile.OpenRead(zipFileName))
            {
                GetBytesFromZipFile(assemblyName, zipFile, out asmbytes, out found, out zipAssemblyName);
                if (loadPDBFile || Debugger.IsAttached)
                {
                    var pdbFileName = assemblyName.Replace("dll", "pdb");
                    GetBytesFromZipFile(pdbFileName, zipFile, out pdbbytes, out pdbfound, out var pdbAssemblyName);
                }
            }

            if (!found)
            {
                throw new ZipAssemblyLoadException(
                    "Assembly specified to load in ZipFile not found.");
            }

            // should only be evaluated if pdb-file is asked for
            if (loadPDBFile && !pdbfound)
            {
                throw new ZipSymbolsLoadException(
                    "pdb to Assembly specified to load in ZipFile not found.");
            }

            // always load pdb when debugging.
            // PDB should be automatically downloaded to zip file always
            // and really *should* always be present.
            var loadPDB = loadPDBFile ? loadPDBFile : Debugger.IsAttached;
            var zipassembly = (ZipAssembly)(Assembly)Load(asmbytes, loadPDB ? pdbbytes : null);
            zipassembly.locationValue = $"{zipFileName}{Path.DirectorySeparatorChar}{zipAssemblyName}";
            return zipassembly;
        }

        [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "A new disposable wrapped in a using block. The code never checks for null at all.", Scope = "member")]
        private static void GetBytesFromZipFile(string entryName, ZipArchive zipFile, out byte[] bytes, out bool found, out string assemblyName)
        {
            var assemblyEntry = zipFile.Entries.FirstOrDefault(e => e.FullName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
            assemblyName = string.Empty;
            found = false;
            bytes = null;
            if (assemblyEntry != null)
            {
                assemblyName = assemblyEntry.FullName;
                found = true;
                using var strm = assemblyEntry.Open();
                using var ms = new MemoryStream();
                strm.CopyTo(ms);
                bytes = ms.ToArray();
            }
        }
    }
}
