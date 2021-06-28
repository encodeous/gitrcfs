using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GitRCFS
{
    public class RcfsNode
    {
        internal Dictionary<string, RcfsNode> _files = new (), _folders = new();
        internal byte[] fileBytes;
        internal byte[] fileHash;
        internal string rootPath;
        public readonly string RelativePath = "";
        public readonly string FileSystemPath;
        public readonly bool IsDirectory;
        public readonly string Name;
        public bool IsDeleted { get; private set; } = false;
        internal RcfsNode(bool isDirectory, string rootPath, string relativePath)
        {
            IsDirectory = isDirectory;
            this.rootPath = rootPath;
            RelativePath = relativePath;
            FileSystemPath = Path.Combine(rootPath, RelativePath);
            if (isDirectory)
            {
                Name = Path.GetDirectoryName(RelativePath);
            }
            else
            {
                Name = Path.GetFileName(RelativePath);
            }
        }
        internal RcfsNode(bool isDirectory, string name)
        {
            Name = name;
            IsDirectory = isDirectory;
        }

        internal bool ApplyChanges()
        {
            bool updated = false;
            var path = Path.Combine(rootPath, RelativePath);
            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                var di = new DirectoryInfo(path);
                if (!di.Exists)
                {
                    RemoveNode();
                    return true;
                }

                var files = di.GetFiles();
                var dirs = di.GetDirectories();

                var relFile = files.Select(x => x.Name).ToHashSet();
                var relDir = dirs.Select(x => x.Name).ToHashSet();
                
                // files
                
                // delete removed files
                foreach (var deleted in (
                    from y in _files
                    where !relFile.Contains(y.Key)
                    select y).ToList())
                {
                    updated = true;
                    deleted.Value.RemoveNode();
                    _files.Remove(deleted.Key);
                }
                // update existing files
                foreach (var file in (
                    from x in relFile
                    where _files.ContainsKey(x)
                    select x).ToList())
                {
                    updated |= _files[file].ApplyChanges();
                }
                // add new files
                foreach (var added in (
                    from x in relFile
                    where !_files.ContainsKey(x)
                    select x).ToList())
                {
                    var val = new RcfsNode(false, rootPath, Path.Combine(RelativePath, added));
                    updated |= val.ApplyChanges();
                    _files[added] = val;
                }

                // folders
                
                // delete removed folders
                foreach (var deleted in (
                    from y in _folders
                    where !relDir.Contains(y.Key)
                    select y).ToList())
                {
                    updated = true;
                    deleted.Value.RemoveNode();
                    _folders.Remove(deleted.Key);
                }
                // update existing folders
                foreach (var file in (
                    from x in relDir
                    where _folders.ContainsKey(x)
                    select x).ToList())
                {
                    updated |= _folders[file].ApplyChanges();
                }
                // add new folders
                foreach (var added in (
                    from x in relDir
                    where !_folders.ContainsKey(x)
                    select x).ToList())
                {
                    var val = new RcfsNode(true, rootPath, Path.Combine(RelativePath, added));
                    updated |= val.ApplyChanges();
                    _folders[added] = val;
                }
            }
            else
            {
                var fi = new FileInfo(path);
                if (!fi.Exists)
                {
                    RemoveNode();
                    return true;
                }

                var tbytes = File.ReadAllBytes(path);
                var hash = SHA256.HashData(tbytes);
                if (fileHash is not null && hash.SequenceEqual(fileHash))
                {
                    return false;
                }

                var oldFile = fileBytes;

                fileHash = hash;
                fileBytes = tbytes;
                
                if (oldFile is not null)
                {
                    ContentsChanged?.Invoke(oldFile, tbytes);
                }
            }

            return updated;
        }
        
        internal void RemoveNode()
        {
            if (!IsDeleted)
            {
                IsDeleted = true;
                NodeRemoved?.Invoke();
                foreach (var f in _files)
                {
                    f.Value.RemoveNode();
                }
                foreach (var f in _folders)
                {
                    f.Value.RemoveNode();
                }
            }
        }

        public delegate void NodeChangedDelegate(byte[] oldValue, byte[] newValue);

        public event NodeChangedDelegate ContentsChanged;
        
        public delegate void NodeUpdatedDelegate();
        public event NodeUpdatedDelegate NodeRemoved;

        public byte[] GetData()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return fileBytes.ToArray();
        }

        public T DeserializeData<T>()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return JsonSerializer.Deserialize<T>(fileBytes);
        }
        
        public string GetStringData()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return Encoding.UTF8.GetString(fileBytes);
        }

        public byte[] GetHash()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return fileHash.ToArray();
        }

        public RcfsNode GetChild(string name)
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            if (_files.ContainsKey(name)) return _files[name];
            if (_folders.ContainsKey(name)) return _folders[name];
            throw new InvalidOperationException("The specified node name does not exist");
        }
        public RcfsNode[] GetFiles()
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            return _files.Values.ToArray();
        }
        public RcfsNode[] GetFolders()
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            return _folders.Values.ToArray();
        }
        public RcfsNode[] GetChildren()
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            return _folders.Values.Union(_files.Values).ToArray();
        }
        /// <summary>
        /// Gets the content of the file
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static implicit operator string(RcfsNode node)
        {
            return node.GetStringData();
        }

        /// <summary>
        /// A shorthand way to get a path / file
        /// </summary>
        /// <param name="node"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static RcfsNode operator /(RcfsNode node, string name)
        {
            return node.GetChild(name);
        }
        /// <summary>
        /// A shorthand way to get a path / file
        /// </summary>
        /// <param name="name"></param>
        public RcfsNode this[string name] => GetChild(name);
    }
}