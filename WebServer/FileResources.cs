
/**
 * File Resources manage the server resources into an efficient way. 
 * It cache the resources in cases it is requested multiple times.
 * Also it check if the requested resources are well formatted.
 **/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections;
using System.Globalization;
using System.Threading;

namespace WebServer
{
    public class Resource
    {

        public DateTime OpenAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public Stream Stream { get; set; }
    }


    public class FileResources
    {
        private class Mapping
        {

            public string AbsolutePath { get; set; }
            public string RelativePath { get; set; }
            public string UriPath { get; set; }
        }

        public static readonly string[] DefaultForbiddenChars = new[] { "..", ":" };

        private static readonly string PathSeparator = Path.DirectorySeparatorChar.ToString();

        private readonly List<Mapping> mappings = new List<Mapping>();

        private Hashtable resources = new Hashtable();

        private int _keepAliveResourceTimeout = 20000000; // 20000 seconds      
        private Timer _keepAliveResource = null;

        public string[] ForbiddenCharacters { get; set; }


        public FileResources(string uri, string absolutePath)
        {
            ForbiddenCharacters = DefaultForbiddenChars;
            _keepAliveResource = new Timer(OnResourceDispose, null, _keepAliveResourceTimeout, _keepAliveResourceTimeout);
            Add(uri, absolutePath);
        }


        ~FileResources()
        {
            if (resources == null) return;
            
            foreach (DictionaryEntry resource in resources)
            {
                string filePath = (string)resource.Key;
                Resource r = (Resource)resource.Value;
                {
                    r.Stream.Close();
                }
            }
            resources.Clear();
        }


        public void Add(string uri, string absolutePath)
        {
            if (!absolutePath.EndsWith(PathSeparator))
                absolutePath += PathSeparator;

            if (!Directory.Exists(absolutePath))
                throw new DirectoryNotFoundException(absolutePath);

            if (!uri.EndsWith("/"))
                uri += "/";
            string relativePath = uri.Replace('/', Path.PathSeparator);
            mappings.Add(new Mapping { AbsolutePath = absolutePath,
                UriPath = uri, RelativePath = relativePath });
        }


        private static bool Contains(string source, IEnumerable<string> chars)
        {
            foreach (string s in chars)
            {
                if (source.Contains(s))
                    return true;
            }

            return false;
        }


        public void FindFiles(string filePath, string searchPattern, List<string> viewNames)
        {
            string[] files = Directory.GetFiles(filePath, searchPattern);
            foreach (string file in files)
                viewNames.Add(Path.GetFileName(file));
        }


        private string GetFullFilePath(string uriPath)
        {
            int pos = uriPath.LastIndexOf('/');
            if (pos == -1)
                return null;
            string path = uriPath.Substring(0, pos + 1);
            string fileName = uriPath.Substring(pos + 1);

            foreach (Mapping mapping in mappings)
            {
                if (!path.StartsWith(mapping.UriPath)) continue;
                path = path.Remove(0, mapping.UriPath.Length);
                path = path.Replace("/", PathSeparator);
                return mapping.AbsolutePath + path + fileName;
            }

            return null;
        }


        public bool Exists(string uriPath)
        {
            if (Contains(uriPath, ForbiddenCharacters))
                return false;

            string filePath = GetFullFilePath(uriPath);

            return filePath != null
                   && File.Exists(filePath)
                   && (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) == 0;
                    // it is not a symlink
        }


        void OnResourceDispose(object state)
        {//todo: Use ConcurrentDictionary from dont.net4
            /*
            lock (resources)
            {
                if (resources == null) return;
                
                Hashtable mySynchedTable = Hashtable.Synchronized(resources);           
                foreach (DictionaryEntry resource in mySynchedTable)
                {
                    string filePath = (string)resource.Key;
                    Resource r = (Resource)resource.Value;
                    //if (r.OpenAt + _keepAliveResourceTimeout) < DateTime.UtcNow)

                    {
                        resources.Remove(resource.Key);
                        r.Stream.Close();
                    }
                }
            }
            _keepAliveResource.Change(_keepAliveResourceTimeout, _keepAliveResourceTimeout);
            * */
        }


        public Resource Get(string uriPath)
        {
            string filePath = GetFullFilePath(uriPath);
            if (filePath == null)
                return null;

            if (Contains(uriPath, ForbiddenCharacters))
                throw new ForbiddenException("Uri contains forbidden characters.");

            try
            {
                Resource r = null;

                if (resources.ContainsKey(filePath))
                {
                    r = (Resource)resources[filePath];
                }
                else
                {

                    if (!File.Exists(filePath))
                        return null;

                    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    r = new Resource
                    {
                        OpenAt = DateTime.UtcNow,
                        ModifiedAt = File.GetLastWriteTime(filePath).ToUniversalTime(),
                        Stream = fileStream
                    };

                    //lock (resources) 
                    {
                        resources[filePath] = r;
                    }
                }
                return r;
            }

            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }

            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }


        public void Find(string path, List<string> viewNames)
        {
            if (path.EndsWith("*"))
            {
                FindByWildCard(path, viewNames);
                return;
            }
            if (!path.EndsWith("/"))
                path += "/";

            if (Contains(path, ForbiddenCharacters))
                throw new ForbiddenException("Uri contains forbidden characters.");

            string diskPath = path.Replace('/', Path.PathSeparator).Remove(0, 1);

            foreach (Mapping mapping in mappings)
            {
                // same directory
                if (mapping.UriPath == path)
                {
                    FindFiles(mapping.AbsolutePath, "*.*", viewNames);
                    return;
                }

                if (!path.StartsWith(mapping.UriPath))
                    continue;

                // ask if is sub diretory
                if (Directory.Exists(mapping.AbsolutePath + diskPath))
                {
                    FindFiles(mapping.AbsolutePath + diskPath, "*.*", viewNames);
                    return;
                }
            }
        }


        private void FindByWildCard(string path, List<string> viewNames)
        {
            string fileName = Path.GetFileName(path);
            path = path.Remove(path.Length - fileName.Length, fileName.Length);
            if (!path.EndsWith("/"))
                path += "/";

            if (Contains(path, ForbiddenCharacters))
                throw new ForbiddenException("Uri contains forbidden characters.");

            string diskPath = path.Replace('/', Path.PathSeparator).Remove(0, 1);

            foreach (Mapping mapping in mappings)
            {
                if (mapping.UriPath == path)
                {
                    FindFiles(mapping.AbsolutePath, fileName, viewNames);
                    return;
                }

                if (!path.StartsWith(mapping.UriPath))
                    continue;

                // ask if sub folder
                string absolutePath = Path.Combine(mapping.AbsolutePath, diskPath);
                if (Directory.Exists(absolutePath))
                {
                    FindFiles(absolutePath, fileName, viewNames);
                    return;
                }
            }
        }
    }

}
