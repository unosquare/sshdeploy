using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Unosquare.Labs.SshDeploy.util
{

    public class CsProjFile
    : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly XDocument _xmlDocument;

        public CsProjNuGetMetadata NuGetMetadata { get; }

        public CsProjFile(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;

            _xmlDocument = XDocument.Load(stream);

            var projectElement = _xmlDocument.Descendants("Project").FirstOrDefault();
            if (projectElement == null || projectElement.Attribute("Sdk")?.Value != "Microsoft.NET.Sdk")
            {
                throw new ArgumentException("Project file is not of the new .csproj type.");
            }

            NuGetMetadata = new CsProjNuGetMetadata(_xmlDocument);
        }

        public void Save()
        {
            _stream.SetLength(0);
            _stream.Position = 0;

            _xmlDocument.Save(_stream);
        }

        public void Dispose()
        {
            if (!_leaveOpen)
            {
                _stream?.Dispose();
            }
        }
    }
}
