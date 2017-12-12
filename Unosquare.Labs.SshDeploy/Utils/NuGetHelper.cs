namespace Unosquare.Labs.SshDeploy.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;
    using Swan;

    internal class NuGetHelper
    {
        internal static readonly string ConfigSection = "config";
        internal static readonly string DefaultGlobalPackagesFolderPath = "packages" + Path.DirectorySeparatorChar;
        
        internal static string GetGlobalPackagesFolder(string filename = null)
        {
            var settings = GetSettingsFromFile(filename);

            var text = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (string.IsNullOrEmpty(text))
            {
                text = settings.GetValue(ConfigSection, "globalPackagesFolder", true);
            }
            else
            {
                VerifyPathIsRooted("NUGET_PACKAGES", text);
            }

            return string.IsNullOrEmpty(text)
                ? Path.Combine(GetFolderPath(NuGetFolderPath.NuGetHome), DefaultGlobalPackagesFolderPath)
                : Path.GetFullPath(text.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }

        internal static string GetFallbackPackageFolder(string filename = null)
        {
            var settings = GetSettingsFromFile(filename);

            return GetFallbackPackageFolders(settings).FirstOrDefault();
        }
        
        internal static string GetFolderPath(NuGetFolderPath folder)
        {
            switch (folder)
            {
                case NuGetFolderPath.MachineWideSettingsBaseDirectory:
                {
                    string folderPath;
                    if (Runtime.OS == Swan.OperatingSystem.Windows)
                    {
                        folderPath = GetFolderPath(SpecialFolder.ProgramFilesX86);
                        if (string.IsNullOrEmpty(folderPath))
                        {
                            folderPath = GetFolderPath(SpecialFolder.ProgramFiles);
                        }
                    }
                    else
                    {
                        folderPath = GetFolderPath(SpecialFolder.CommonApplicationData);
                    }

                    return Path.Combine(folderPath, "NuGet");
                }

                case NuGetFolderPath.MachineWideConfigDirectory:
                    return Path.Combine(GetFolderPath(NuGetFolderPath.MachineWideSettingsBaseDirectory), "Config");
                case NuGetFolderPath.UserSettingsDirectory:
                    return Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "NuGet");
                case NuGetFolderPath.NuGetHome:
                    return Path.Combine(GetFolderPath(SpecialFolder.UserProfile), ".nuget");
                default:
                    return null;
            }
        }

        private static IReadOnlyList<string> GetFallbackPackageFolders(NuGetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var list = new List<string>();
            var environmentVariable = Environment.GetEnvironmentVariable("NUGET_FALLBACK_PACKAGES");
            if (string.IsNullOrEmpty(environmentVariable))
            {
                list.AddRange(settings.GetSettingValues("fallbackPackageFolders", true).Select(x => x.Value));
            }
            else
            {
                list.AddRange(environmentVariable.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (var current in list)
                {
                    VerifyPathIsRooted("NUGET_FALLBACK_PACKAGES", current);
                }
            }

            for (var i = 0; i < list.Count; i++)
            {
                list[i] = list[i].Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                list[i] = Path.GetFullPath(list[i]);
            }

            return list;
        }

        private static NuGetSettings GetSettingsFromFile(string filename = null)
        {
            if (filename == null)
            {
                filename = Directory
                    .EnumerateFiles(Directory.GetCurrentDirectory(), "*.csproj", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentNullException(nameof(filename));

            var settings = NuGetSettings.LoadDefaultSettings(Path.GetDirectoryName(filename));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            
            return settings;
        }

        private static string GetHome()
        {
            var environmentVariable = Environment.GetEnvironmentVariable("USERPROFILE") ??
                                      Environment.GetEnvironmentVariable("HOME");

            return string.IsNullOrEmpty(environmentVariable)
                ? Environment.GetEnvironmentVariable("HOMEDRIVE") + Environment.GetEnvironmentVariable("HOMEPATH")
                : environmentVariable;
        }

        private static string GetFolderPath(SpecialFolder folder)
        {
            Environment.SpecialFolder folder2;
            switch (folder)
            {
                case SpecialFolder.ProgramFilesX86:
                    folder2 = Environment.SpecialFolder.ProgramFilesX86;
                    break;
                case SpecialFolder.ProgramFiles:
                    folder2 = Environment.SpecialFolder.ProgramFiles;
                    break;
                case SpecialFolder.UserProfile:
                {
                    var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return !string.IsNullOrEmpty(folderPath) ? folderPath : GetHome();
                }

                case SpecialFolder.CommonApplicationData:
                    folder2 = Environment.SpecialFolder.CommonApplicationData;
                    break;
                case SpecialFolder.ApplicationData:
                    folder2 = Environment.SpecialFolder.ApplicationData;
                    break;
                default:
                    return null;
            }

            return Environment.GetFolderPath(folder2);
        }

        private static void VerifyPathIsRooted(string key, string path)
        {
            if (!Path.IsPathRooted(path))
            {
                throw new Exception($"Invalid {key}");
            }
        }
    }

    /// <summary>
    /// Concrete implementation of Settings to support NuGet Settings
    /// </summary>
    internal class NuGetSettings
    {
        /// <summary>
        /// Default file name for a settings file is 'NuGet.config'
        /// Also, the machine level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
        internal static readonly string DefaultSettingsFileName = "NuGet.Config";

        internal static readonly string AddV3TrackFile = "nugetorgadd.trk";

        internal static readonly string FeedName = "nuget.org";
        internal static readonly string V3FeedUrl = "https://api.nuget.org/v3/index.json";
        internal static readonly string ProtocolVersionAttribute = "protocolVersion";
        internal static readonly string DisabledPackageSources = "disabledPackageSources";
        internal static readonly string PackageSources = "packageSources";

        internal static readonly string KeyAttribute = "key";
        internal static readonly string ValueAttribute = "value";

        /// <summary>
        /// NuGet config names with casing ordered by precedence.
        /// </summary>
        internal static readonly string[] OrderedSettingsFileNames;

        internal static readonly string[] SupportedMachineWideConfigExtension;

        private NuGetSettings _next;

        private int _priority;

        private XDocument ConfigXDocument { get; }

        internal string FileName { get; }

        private bool IsMachineWideSettings { get; }

        private bool Cleared { get; set; }

        /// <summary>
        /// Folder under which the config file is present
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Full path to the ConfigFile corresponding to this Settings object
        /// </summary>
        internal string ConfigFilePath => Path.GetFullPath(Path.Combine(Root, FileName));

        internal NuGetSettings(string root)
            : this(root, DefaultSettingsFileName)
        {
        }

        internal NuGetSettings(string root, string fileName, bool isMachineWideSettings = false)
        {
            Root = root;
            FileName = fileName;
            IsMachineWideSettings = isMachineWideSettings;
            ConfigXDocument = GetDocument(ConfigFilePath);
            CheckConfigRoot();
        }

        private static XDocument GetDocument(string fullPath)
        {
            XDocument result;
            using (Stream stream = File.OpenRead(fullPath))
            {
                result = LoadSafe(stream, LoadOptions.PreserveWhitespace);
            }

            return result;
        }

        private static XDocument LoadSafe(Stream input, LoadOptions options)
        {
            return XDocument.Load(XmlReader.Create(input, CreateSafeSettings()), options);
        }

        private static XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
        {
            return new XmlReaderSettings
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = ignoreWhiteSpace
            };
        }

        internal static NuGetSettings LoadDefaultSettings(string root,
            XPlatMachineWideSetting machineWideSettings = null)
        {
            var list = new List<NuGetSettings>();
            if (root != null)
            {
                list.AddRange(GetSettingsFileNames(root).Select(f => ReadSettings(root, f)).Where(x => x != null));
            }

            return LoadSettingsForSpecificConfigs(root, list, machineWideSettings ?? new XPlatMachineWideSetting());
        }

        internal static NuGetSettings LoadSettingsForSpecificConfigs(string root, List<NuGetSettings> validSettingFiles,
            XPlatMachineWideSetting machineWideSettings)
        {
            LoadUserSpecificSettings(validSettingFiles, root, machineWideSettings);

            if (machineWideSettings != null)
            {
                validSettingFiles.AddRange(machineWideSettings.Settings.Select(s =>
                    new NuGetSettings(s.Root, s.FileName, s.IsMachineWideSettings)));
            }

            if (validSettingFiles == null || !validSettingFiles.Any())
            {
                return new NuGetSettings(root);
            }

            SetClearTagForSettings(validSettingFiles);
            validSettingFiles[0]._priority = validSettingFiles.Count;

            for (var i = 1; i < validSettingFiles.Count; i++)
            {
                validSettingFiles[i]._next = validSettingFiles[i - 1];
                validSettingFiles[i]._priority = validSettingFiles[i - 1]._priority - 1;
            }

            return validSettingFiles.Last();
        }

        private static void LoadUserSpecificSettings(List<NuGetSettings> validSettingFiles, string root,
            XPlatMachineWideSetting machineWideSettings)
        {
            if (root == null)
            {
                root = string.Empty;
            }

            NuGetSettings settings;
            var folderPath = NuGetHelper.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            if (folderPath == null)
            {
                return;
            }

            var text = Path.Combine(folderPath, DefaultSettingsFileName);
            if (!File.Exists(text) && machineWideSettings != null)
            {
                settings = ReadSettings(root, text);
                var list = new List<SettingValue>();

                foreach (var current in machineWideSettings.Settings)
                {
                    list.AddRange(current.GetSettingValues(PackageSources, true)
                        .Where(current2 => current2.Value.StartsWith("http"))
                        .Select(current2 => new SettingValue(current2.Key, "true", current)));
                }

                settings.UpdateSections(DisabledPackageSources, list);
            }
            else
            {
                settings = ReadSettings(root, text);
                if (!settings.GetSettingValues(PackageSources).Any())
                {
                    var path = Path.Combine(Path.GetDirectoryName(text), AddV3TrackFile);

                    if (!File.Exists(path))
                    {
                        File.Create(path).Dispose();
                        var settingValue = new SettingValue(FeedName, V3FeedUrl);
                        settingValue.AdditionalData.Add(ProtocolVersionAttribute, "3");
                        settings.UpdateSections(PackageSources, new List<SettingValue> {settingValue});
                    }
                }
            }

            validSettingFiles.Add(settings);
        }

        internal string GetValue(string section, string key, bool isPath = false)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException();
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException();
            }

            XElement xElement = null;
            string result = null;
            for (var settings = this; settings != null; settings = settings._next)
            {
                var valueInternal = settings.GetValueInternal(section, key, xElement);

                if (xElement != valueInternal)
                {
                    xElement = valueInternal;
                    result = settings.ElementToValue(xElement, isPath);
                }
            }

            return result;
        }

        private string ApplyEnvironmentTransform(string configValue)
            => string.IsNullOrEmpty(configValue) ? configValue : Environment.ExpandEnvironmentVariables(configValue);

        internal IList<SettingValue> GetSettingValues(string section, bool isPath = false)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException();
            }

            var list = new List<SettingValue>();
            for (var settings = this; settings != null; settings = settings._next)
            {
                settings.PopulateValues(section, list, isPath);
            }

            return list.AsReadOnly();
        }

        internal void UpdateSections(string section, IReadOnlyList<SettingValue> values)
        {
            if (IsMachineWideSettings ||
                ((section == PackageSources ||
                  section == DisabledPackageSources) && Cleared))
            {
                if (_next == null)
                {
                    throw new InvalidOperationException();
                }

                _next.UpdateSections(section, values);
                return;
            }

            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException();
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var enumerable = (_next == null)
                ? values
                : values.Where(v => v.Priority < _next._priority);

            var xElement = GetSection(ConfigXDocument.Root, section);
            if (xElement == null && enumerable.Any())
            {
                xElement = GetOrCreateSection(ConfigXDocument.Root, section);
            }

            RemoveElementAfterClearTag(xElement);

            foreach (var current in enumerable)
            {
                var xElement2 = new XElement("add");
                SetElementValues(xElement2, current.Key, current.OriginalValue, current.AdditionalData);
                XElementUtility.AddIndented(xElement, xElement2);
            }

            _next?.UpdateSections(section, values.Where(v => v.Priority >= _next._priority).ToList());
        }

        private static void RemoveElementAfterClearTag(XElement sectionElement)
        {
            if (sectionElement == null)
            {
                return;
            }

            var list = new List<XNode>();
            foreach (var current in sectionElement.Nodes())
            {
                if (current.NodeType != XmlNodeType.Element)
                {
                    list.Add(current);
                }
                else
                {
                    var xElement = (XElement) current;
                    if (xElement.Name.LocalName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Clear();
                    }
                    else
                    {
                        list.Add(xElement);
                    }
                }
            }

            IEnumerable<XNode> arg930 = list;
            if (arg930.Any(x => x != null))
            {
                using (var enumerator2 = list.GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        enumerator2.Current.Remove();
                    }
                }
            }
        }

        private static void SetElementValues(XElement element, string key, string value,
            IDictionary<string, string> attributes)
        {
            foreach (var current in element.Attributes())
            {
                if (!string.Equals(current.Name.LocalName, KeyAttribute, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(current.Name.LocalName, ValueAttribute, StringComparison.OrdinalIgnoreCase) &&
                    !attributes.ContainsKey(current.Name.LocalName))
                {
                    current.Remove();
                }
            }

            element.SetAttributeValue(KeyAttribute, key);
            element.SetAttributeValue(ValueAttribute, value);

            if (attributes == null) return;

            foreach (var current2 in attributes)
            {
                element.SetAttributeValue(current2.Key, current2.Value);
            }
        }

        private XElement GetValueInternal(string section, string key, XElement curr)
        {
            var section2 = GetSection(ConfigXDocument.Root, section);
            return section2 == null ? curr : FindElementByKey(section2, key, curr);
        }

        private static XElement GetSection(XElement parentElement, string section)
        {
            section = XmlConvert.EncodeLocalName(section);
            return parentElement.Element(section);
        }

        private static XElement GetOrCreateSection(XElement parentElement, string sectionName)
        {
            sectionName = XmlConvert.EncodeLocalName(sectionName);
            var xElement = parentElement.Element(sectionName);

            if (xElement == null)
            {
                xElement = new XElement(sectionName);
                XElementUtility.AddIndented(parentElement, xElement);
            }

            return xElement;
        }

        private static XElement FindElementByKey(XElement sectionElement, string key, XElement curr)
        {
            var result = curr;
            foreach (var current in sectionElement.Elements())
            {
                var localName = current.Name.LocalName;
                if (localName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    result = null;
                }
                else if (localName.Equals("add", StringComparison.OrdinalIgnoreCase) && XElementUtility
                             .GetOptionalAttributeValue(current, KeyAttribute)
                             .Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    result = current;
                }
            }

            return result;
        }

        private string ElementToValue(XElement element, bool isPath)
        {
            if (element == null)
            {
                return null;
            }

            var text = XElementUtility.GetOptionalAttributeValue(element, ValueAttribute);
            text = ApplyEnvironmentTransform(text);

            return !isPath || string.IsNullOrEmpty(text)
                ? text
                : Path.Combine(Root, ResolvePath(Path.GetDirectoryName(ConfigFilePath), text));
        }

        private static string ResolvePath(string configDirectory, string value)
        {
            var pathRoot = Path.GetPathRoot(value);
            if (pathRoot != null && pathRoot.Length == 1 &&
                (pathRoot[0] == Path.DirectorySeparatorChar || value[0] == Path.AltDirectorySeparatorChar))
            {
                return Path.Combine(Path.GetPathRoot(configDirectory), value.Substring(1));
            }

            return Path.Combine(configDirectory, value);
        }

        private void PopulateValues(string section, List<SettingValue> current, bool isPath)
        {
            if (ConfigXDocument == null) return;
            var section2 = GetSection(ConfigXDocument.Root, section);
            if (section2 != null)
            {
                ReadSection(section2, current, isPath);
            }
        }

        private void ReadSection(XContainer sectionElement, ICollection<SettingValue> values, bool isPath)
        {
            foreach (var current in sectionElement.Elements())
            {
                var localName = current.Name.LocalName;
                if (localName.Equals("add", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(ReadSettingsValue(current, isPath));
                }
                else if (localName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    values.Clear();
                }
            }
        }

        private SettingValue ReadSettingsValue(XElement element, bool isPath)
        {
            var xAttribute = element.Attribute(KeyAttribute);
            var xAttribute2 = element.Attribute(ValueAttribute);

            if (string.IsNullOrEmpty(xAttribute?.Value) || xAttribute2 == null)
            {
                throw new InvalidDataException();
            }

            var text = ApplyEnvironmentTransform(xAttribute2.Value);
            var value = xAttribute2.Value;
            if (isPath && Uri.TryCreate(text, UriKind.Relative, out _))
            {
                var directoryName = Path.GetDirectoryName(ConfigFilePath);
                text = Path.Combine(Root, Path.Combine(directoryName, text));
            }

            var settingValue =
                new SettingValue(xAttribute.Value, text, this, IsMachineWideSettings, value, _priority);
            foreach (var current in element.Attributes())
            {
                if (!string.Equals(current.Name.LocalName, KeyAttribute, StringComparison.Ordinal) &&
                    !string.Equals(current.Name.LocalName, ValueAttribute, StringComparison.Ordinal))
                {
                    settingValue.AdditionalData[current.Name.LocalName] = current.Value;
                }
            }

            return settingValue;
        }

        internal static NuGetSettings ReadSettings(string root, string settingsPath, bool isMachineWideSettings = false)
        {
            try
            {
                var expr07 = GetFileNameAndItsRoot(root, settingsPath);
                var item = expr07.Item1;
                root = expr07.Item2;
                return new NuGetSettings(root, item, isMachineWideSettings);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        internal static bool IsPathAFile(string path)
            => string.Equals(path, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);

        internal static Tuple<string, string> GetFileNameAndItsRoot(string root, string settingsPath)
        {
            string item;
            if (Path.IsPathRooted(settingsPath))
            {
                root = Path.GetDirectoryName(settingsPath);
                item = Path.GetFileName(settingsPath);
            }
            else if (!IsPathAFile(settingsPath))
            {
                var expr33 = Path.Combine(root ?? string.Empty, settingsPath);
                root = Path.GetDirectoryName(expr33);
                item = Path.GetFileName(expr33);
            }
            else
            {
                item = settingsPath;
            }

            return new Tuple<string, string>(item, root);
        }

        private static IEnumerable<string> GetSettingsFileNames(string root)
        {
            using (var e = GetSettingsFilePaths(root).GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var settingsFileNameFromDir = GetSettingsFileNameFromDir(e.Current);
                    if (settingsFileNameFromDir != null)
                    {
                        yield return settingsFileNameFromDir;
                    }
                }
            }
        }

        private static string GetSettingsFileNameFromDir(string directory)
        {
            return OrderedSettingsFileNames.Select(path => Path.Combine(directory, path)).FirstOrDefault(File.Exists);
        }

        private static IEnumerable<string> GetSettingsFilePaths(string root)
        {
            while (root != null)
            {
                yield return root;
                root = Path.GetDirectoryName(root);
            }
        }

        private static void SetClearTagForSettings(List<NuGetSettings> settings)
        {
            var flag = false;

            foreach (var current in settings)
            {
                if (!flag)
                {
                    flag = FoundClearTag(current.ConfigXDocument);
                }
                else
                {
                    current.Cleared = true;
                }
            }
        }

        private static bool FoundClearTag(XDocument config)
        {
            var section = GetSection(config.Root, PackageSources);
            if (section == null) return false;

            using (var enumerator = section.Elements().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Name.LocalName.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void CheckConfigRoot()
        {
            if (ConfigXDocument.Root?.Name != "configuration")
            {
                throw new Exception();
            }
        }

        static NuGetSettings()
        {
            // Note: this type is marked as 'beforefieldinit'.
            string[] arg_3F0;
            if (Runtime.OS != Swan.OperatingSystem.Windows)
            {
                var expr17 = new string[3];
                expr17[0] = "nuget.config";
                expr17[1] = "NuGet.config";
                arg_3F0 = expr17;
                expr17[2] = DefaultSettingsFileName;
            }
            else
            {
                (arg_3F0 = new string[1])[0] = DefaultSettingsFileName;
            }

            OrderedSettingsFileNames = arg_3F0;

            SupportedMachineWideConfigExtension = Runtime.OS != Swan.OperatingSystem.Windows
                ? new[] {"*.Config", "*.config"}
                : new[] {"*.config"};
        }

        /// <summary>
        /// Machine wide settings based on the default machine wide config directory.
        /// </summary>
        internal class XPlatMachineWideSetting
        {
            private readonly Lazy<IEnumerable<NuGetSettings>> _settings;

            internal XPlatMachineWideSetting()
            {
                var baseDirectory = NuGetHelper.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
                _settings = new Lazy<IEnumerable<NuGetSettings>>(() => LoadMachineWideSettings(baseDirectory));
            }

            internal IEnumerable<NuGetSettings> Settings => _settings.Value;

            private static string EnsureTrailingCharacter(string path, char trailingCharacter)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                return path.Length == 0 || path[path.Length - 1] == trailingCharacter ? path : path + trailingCharacter;
            }

            internal static string GetRelativePath(string root, string fullPath)
                => fullPath.Substring(root.Length).TrimStart(new[] {Path.DirectorySeparatorChar});

            internal static IEnumerable<string> GetFilesRelativeToRoot(
                string root,
                string path,
                string[] filters = null,
                SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                path = EnsureTrailingCharacter(Path.Combine(root, path), Path.DirectorySeparatorChar);
                if (filters == null || !filters.Any())
                {
                    filters = new[] {"*.*"};
                }

                try
                {
                    if (!Directory.Exists(path))
                    {
                        return Enumerable.Empty<string>();
                    }

                    var hashSet = new HashSet<string>();
                    var array = filters;

                    foreach (var searchPattern in array)
                    {
                        var other = Directory.EnumerateFiles(path, searchPattern, searchOption);
                        hashSet.UnionWith(other);
                    }

                    return hashSet.Select(f => GetRelativePath(root, f));
                }
                catch (UnauthorizedAccessException)
                {
                    // ignore
                }
                catch (DirectoryNotFoundException)
                {
                    // ignore
                }

                return Enumerable.Empty<string>();
            }

            internal static IEnumerable<NuGetSettings> LoadMachineWideSettings(string root, params string[] paths)
            {
                if (string.IsNullOrEmpty(root))
                {
                    throw new ArgumentException("root cannot be null or empty");
                }

                var list = new List<NuGetSettings>();
                var text = Path.Combine(paths);

                while (true)
                {
                    list.AddRange(GetFilesRelativeToRoot(root, text, SupportedMachineWideConfigExtension)
                        .Select(current => ReadSettings(root, current, true)).Where(settings => settings != null));

                    if (text.Length == 0)
                    {
                        break;
                    }

                    var num = text.LastIndexOf(Path.DirectorySeparatorChar);
                    if (num < 0)
                    {
                        num = 0;
                    }

                    text = text.Substring(0, num);
                }

                return list;
            }
        }
    }

    internal static class XElementUtility
    {
        internal static string GetOptionalAttributeValue(XElement element, string localName,
            string namespaceName = null)
        {
            var xAttribute = element.Attribute(string.IsNullOrEmpty(namespaceName)
                ? localName
                : XName.Get(localName, namespaceName));

            return xAttribute?.Value;
        }

        internal static void AddIndented(XContainer container, XContainer content)
        {
            var text = ComputeOneLevelOfIndentation(container);
            var text2 = (container.PreviousNode is XText xText) ? xText.Value : Environment.NewLine;
            IndentChildrenElements(content, text2 + text, text);
            AddLeadingIndentation(container, text2, text);
            container.Add(content);
            AddTrailingIndentation(container, text2);
        }

        internal static void RemoveIndented(XNode element)
        {
            var xText = element.PreviousNode as XText;
            var xText2 = element.NextNode as XText;
            var text = ComputeOneLevelOfIndentation(element);
            var arg440 = !element.ElementsAfterSelf().Any();
            element.Remove();
            if (xText2 != null && IsWhiteSpace(xText2))
            {
                xText2.Remove();
            }

            if (arg440 && xText != null && IsWhiteSpace(xText2))
            {
                xText.Value = xText.Value.Substring(0, xText.Value.Length - text.Length);
            }
        }

        private static string ComputeOneLevelOfIndentation(XNode node)
        {
            var num = node.Ancestors().Count();
            if (num == 0 || !(node.PreviousNode is XText xText) || !IsWhiteSpace(xText))
            {
                return "  ";
            }

            var text = xText.Value.Trim(Environment.NewLine.ToCharArray());
            var arg620 = (text.LastOrDefault() == '\t') ? '\t' : ' ';
            var count = Math.Max(1, text.Length / num);
            return new string(arg620, count);
        }

        private static bool IsWhiteSpace(XText textNode) => string.IsNullOrWhiteSpace(textNode.Value);

        private static void IndentChildrenElements(XContainer container, string containerIndent, string oneIndentLevel)
        {
            var text = containerIndent + oneIndentLevel;
            foreach (var expr_1C in container.Elements())
            {
                expr_1C.AddBeforeSelf(new XText(text));
                IndentChildrenElements(expr_1C, text + oneIndentLevel, oneIndentLevel);
            }

            if (container.Elements().Any())
            {
                container.Add(new XText(containerIndent));
            }
        }

        private static void AddLeadingIndentation(XContainer container, string containerIndent, string oneIndentLevel)
        {
            if (!container.Nodes().Any() || !(container.LastNode is XText xText))
            {
                container.Add(new XText(containerIndent + oneIndentLevel));
                return;
            }

            xText.Value += oneIndentLevel;
        }

        private static void AddTrailingIndentation(XContainer container, string containerIndent) =>
            container.Add(new XText(containerIndent));
    }

    /// <summary>
    /// Represents a single setting value in a settings file
    /// </summary>
    internal class SettingValue
    {
        internal SettingValue(string key, string value, bool isMachineWide = false, int priority = 0)
            : this(key, value, null, isMachineWide, value, priority)
        {
        }

        internal SettingValue(string key, string value, NuGetSettings origin, bool isMachineWide = true,
            int priority = 0)
            : this(key, value, origin, isMachineWide, value, priority)
        {
        }

        internal SettingValue(string key, string value, NuGetSettings origin, bool isMachineWide, string originalValue,
            int priority = 0)
        {
            Key = key;
            Value = value;
            Origin = origin;
            IsMachineWide = isMachineWide;
            Priority = priority;
            OriginalValue = originalValue;
            AdditionalData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Represents the key of the setting
        /// </summary>
        internal string Key { get; }

        /// <summary>
        /// Represents the value of the setting
        /// </summary>
        internal string Value { get; set; }

        /// <summary>
        /// original value of the source as in NuGet.Config
        /// </summary>
        internal string OriginalValue { get; set; }

        /// <summary>
        /// IsMachineWide tells if the setting is machine-wide or not
        /// </summary>
        internal bool IsMachineWide { get; set; }

        /// <summary>
        /// The priority of this setting in the nuget.config hierarchy. Bigger number means higher priority
        /// </summary>
        internal int Priority { get; set; }

        /// <summary>
        /// Gets the <see cref="T:NuGet.Configuration.Settings" /> that provided this value.
        /// </summary>
        internal NuGetSettings Origin { get; }

        /// <summary>
        /// Gets additional values with the specified setting.
        /// </summary>
        /// <remarks>
        /// When reading from an XML based settings file, this includes all attributes on the element
        /// other than the <c>Key</c> and <c>Value</c>.
        /// </remarks>
        internal IDictionary<string, string> AdditionalData { get; }
    }

    internal enum NuGetFolderPath
    {
        MachineWideSettingsBaseDirectory,
        MachineWideConfigDirectory,
        UserSettingsDirectory,
        NuGetHome
    }

    internal enum SpecialFolder
    {
        ProgramFilesX86,
        ProgramFiles,
        UserProfile,
        CommonApplicationData,
        ApplicationData
    }
}