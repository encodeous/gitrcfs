using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitRCFS
{
    public class RcfsNode
    {
        internal Dictionary<string, RcfsNode> _files = new (), _dirs = new();
        public IEnumerable<string> Files => _files.Keys;
        public IEnumerable<string> Directories => _dirs.Keys;
        internal byte[] fileBytes;
        internal byte[] fileHash;
        internal string rootPath;
        public readonly string RelativePath = "";
        public readonly string FileSystemPath;
        private ILogger<RcfsNode> _logger;
        public bool IsDirectory { get; }
        public string Name { get; }
        public bool IsDeleted { get; private set; } = false;
        internal RcfsNode(bool isDirectory, string rootPath, string relativePath, ILogger<RcfsNode> logger)
        {
            if (logger is null)
                logger = new NullLogger<RcfsNode>();
            _logger = logger;
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
        internal RcfsNode(bool isDirectory, string name, ILogger<RcfsNode> logger)
        {
            if (logger is null)
                logger = new NullLogger<RcfsNode>();
            _logger = logger;
            Name = name;
            IsDirectory = isDirectory;
        }

        internal bool ApplyChanges()
        {
            try
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
                        var val = new RcfsNode(false, rootPath, Path.Combine(RelativePath, added), _logger);
                        updated |= val.ApplyChanges();
                        _files[added] = val;
                    }

                    // directories

                    // delete removed directories
                    foreach (var deleted in (
                                 from y in _dirs
                                 where !relDir.Contains(y.Key)
                                 select y).ToList())
                    {
                        updated = true;
                        deleted.Value.RemoveNode();
                        _dirs.Remove(deleted.Key);
                    }

                    // update existing directories
                    foreach (var file in (
                                 from x in relDir
                                 where _dirs.ContainsKey(x)
                                 select x).ToList())
                    {
                        updated |= _dirs[file].ApplyChanges();
                    }

                    // add new directories
                    foreach (var added in (
                                 from x in relDir
                                 where !_dirs.ContainsKey(x)
                                 select x).ToList())
                    {
                        var val = new RcfsNode(true, rootPath, Path.Combine(RelativePath, added), _logger);
                        updated |= val.ApplyChanges();
                        _dirs[added] = val;
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
                    updated = true;

                    if (oldFile is not null)
                    {
                        ContentsChanged?.Invoke(oldFile, tbytes);
                    }
                }

                if(updated)
                    NodeChanged?.Invoke();
                
                return updated;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed applying changes to RCFS node {Name}", Name);
            }

            return false;
        }
        
        internal void RemoveNode()
        {
            if (!IsDeleted)
            {
                IsDeleted = true;
                _logger.LogTrace("Removed node {Name} from RCFS tree", Name);
                NodeRemoved?.Invoke();
                NodeChanged?.Invoke();
                foreach (var f in _files)
                {
                    f.Value.RemoveNode();
                }
                foreach (var f in _dirs)
                {
                    f.Value.RemoveNode();
                }
            }
        }
        
        /// <summary>
        /// A delegate that is called when the values of a node is changed
        /// </summary>
        public delegate void NodeChangedDelegate();

        /// <summary>
        /// Called when the contents of this current node is changed
        /// </summary>
        public event NodeChangedDelegate NodeChanged;
        
        /// <summary>
        /// A delegate that is called when the values of a file is changed
        /// </summary>
        public delegate void NodeContentChangedDelegate(byte[] oldValue, byte[] newValue);

        /// <summary>
        /// Called when the contents of this current file is changed
        /// </summary>
        public event NodeContentChangedDelegate ContentsChanged;
        
        /// <summary>
        /// A delegate that is called when the node is updated in some way
        /// </summary>
        public delegate void NodeUpdatedDelegate();
        /// <summary>
        /// Called when the current node is deleted
        /// </summary>
        public event NodeUpdatedDelegate NodeRemoved;

        /// <summary>
        /// Gets the raw binary data in this file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public byte[] GetData()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return fileBytes.ToArray();
        }

        /// <summary>
        /// Tries to deserialize the file using System.Text.Json
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public T DeserializeData<T>()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return JsonSerializer.Deserialize<T>(fileBytes);
        }
        
        /// <summary>
        /// Gets the file as a string encoded in UTF-8
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public string GetStringData()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return Encoding.UTF8.GetString(fileBytes);
        }

        /// <summary>
        /// Gets the SHA-256 hash of the current file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public byte[] GetHash()
        {
            if (IsDirectory) throw new InvalidOperationException("Cannot access data of a directory");
            return fileHash.ToArray();
        }

        /// <summary>
        /// Resolves a path
        /// </summary>
        /// <param name="splitPath">The path segments</param>
        /// <returns></returns>
        public RcfsNode ResolvePath(string[] splitPath)
        {
            var curNode = this;
            for (int i = 0; i < splitPath.Length; i++)
            {
                curNode = curNode.GetChild(splitPath[i]);
            }
            return curNode;
        }

        /// <summary>
        /// Resolves a path with path segments separated with "/"
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public RcfsNode ResolvePath(string path) => ResolvePath(path.Split("/"));

        /// <summary>
        /// Gets a direct descendant of the current node
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public RcfsNode GetChild(string name)
        {
            if (name == string.Empty) return this;
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            if (_files.ContainsKey(name)) return _files[name];
            if (_dirs.ContainsKey(name)) return _dirs[name];
            throw new InvalidOperationException($"The specified node \"{name}\" does not exist");
        }
        /// <summary>
        /// Gets all the direct descendants of the current directory that is a file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public RcfsNode[] GetFiles()
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            return _files.Values.ToArray();
        }
        /// <summary>
        /// Gets all the direct descendants of the current directory that is a directory
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public RcfsNode[] GetDirectories()
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            return _dirs.Values.ToArray();
        }
        /// <summary>
        /// Gets all the direct descendants of the current directory
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public RcfsNode[] GetChildren()
        {
            if (!IsDirectory) throw new InvalidOperationException("Cannot access children of a file");
            return _dirs.Values.Union(_files.Values).ToArray();
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
        /// A shorthand way to get a path or file
        /// </summary>
        /// <param name="node"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static RcfsNode operator /(RcfsNode node, string name)
        {
            return node.GetChild(name);
        }
        /// <summary>
        /// A shorthand way to get a path or file
        /// </summary>
        /// <param name="path"></param>
        public RcfsNode this[string path] => ResolvePath(path);
    }
}