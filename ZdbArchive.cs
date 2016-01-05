using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace SocomFiles
{
    class ZdbArchive
    {
        public string ArchivePath { get; set; }
        public string OutputPath { get; set; }
        public List<SocomFile> Files { get; set; }
        public byte[] FileData { get; set; } 

        public void Package(string archivepath, string outputPath)
        {
            var files = Directory.GetFiles(archivepath, ".", SearchOption.AllDirectories)
                .OrderByDescending(x => Regex.IsMatch(x, @"READERM\.ZAR"))
                    .ThenByDescending(x => Regex.IsMatch(x, @"[A-Z]{2}[0-9]{2}LOC\.ZAR"));

            var archiveHeader = File.ReadAllBytes(@"zdbHeader.bin").ToList();

            InsertBytes(archiveHeader, BitConverter.GetBytes(files.Count()), 152);
            AppendAllBytes(outputPath + "result.zdb", archiveHeader.ToArray());

            var address = 0;
            var headerSize = 92;

            foreach (var filePath in files) {

                var file = filePath.Replace(archivepath, string.Empty);

                var temp = new byte[headerSize];
                var fileHeader = temp.ToList();

                InsertBytes(fileHeader, BitConverter.GetBytes(headerSize), 0);
                
                InsertBytes(fileHeader, StringToBytes(file), 4);

                var pointer = BitConverter.GetBytes((files.Count() * headerSize) + address + archiveHeader.Count());
                InsertBytes(fileHeader, pointer, 68);

                var fileSize = (int)new FileInfo(filePath).Length;
                var fileSizeBytes = BitConverter.GetBytes(fileSize);
                InsertBytes(fileHeader, fileSizeBytes, 72);
                
                AppendAllBytes(outputPath + "result.zdb", fileHeader.ToArray());
                
                address += fileSize;
            }

            foreach (var filePath in files)
                AppendAllBytes(outputPath + "result.zdb", File.ReadAllBytes(filePath));
            
        }

        public void Extract(string archivepath, string outputPath)
        {
            if (File.Exists(archivepath))
                FileData = File.ReadAllBytes(archivepath);
            else
                return;

            ArchivePath = archivepath;
            OutputPath = outputPath;

            var fileCount = ReadInt(FileData, 152);
            var pathOffset = ReadInt(FileData, 156);
            var baseAddress = 164;

            for(var i = 0; i < fileCount; i++)
            {
                var path = ReadString(FileData, (i * pathOffset) + 164);
                var pointer = ReadInt(FileData, baseAddress + (i * pathOffset) + 64);
                var size = ReadInt(FileData, baseAddress + (i * pathOffset) + 68);
                CreateDirectories(path);
                CreateFile(ReadFile(path, FileData, pointer, size));
            }

        }

        private void CreateDirectories(string path)
        {
            var directories = path.Split(new string[] { @"\" }, StringSplitOptions.RemoveEmptyEntries);
            for(var i = 0; i < directories.Count(); i++)
            {
                var currectDirectory = OutputPath + String.Join(@"\", directories.Take(i));
               if(!Directory.Exists(currectDirectory))
                    Directory.CreateDirectory(currectDirectory);
            }
        }

        private void InsertBytes(List<byte> data, byte[] bytes, int i)
        {
            data.RemoveRange(i, bytes.Count());
            data.InsertRange(i, bytes);
        }

        private void CreateFile(SocomFile file)
        {
            if (!File.Exists(OutputPath + file.Path))
                File.WriteAllBytes(OutputPath + file.Path, file.Data);
        }

        private SocomFile ReadFile(string path, byte[] data, int address, int length)
        {
            var fileData = new List<byte>();
            for(var i = address; i < address + length; i++)
                fileData.Add(data[i]);

            return new SocomFile()
            {
                Path = path,
                Extension = Path.GetExtension(path).ToLower().Replace(".", string.Empty),
                Size = length,
                Data = fileData.ToArray()
            };
            
        }

        private int ReadInt(byte[] data, int i) => Convert.ToInt32(ByteToHex(ChangeEndian(new byte[] { FileData[i], FileData[i + 1], FileData[i + 2], FileData[i + 3] })), 16);
        
        private string ReadString(byte[] data, int i)
        {
            var byteBuffer = new List<byte>();
            while (data[i] != 0)
            {
                byteBuffer.Add(data[i]);
                i++;
            }

            return Encoding.ASCII.GetString(byteBuffer.ToArray());
        }

        private byte[] StringToBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes.Where(x => x != 0).ToArray();
        }

        private string ByteToHex(byte[] data) => BitConverter.ToString(data).Replace("-", string.Empty);
        
        private byte[] ChangeEndian(byte[] data) => new byte[] { data[3], data[2], data[1], data[0] };


        private static void AppendAllBytes(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }
    }

    public class SocomFile
    {
        public string Path { get; set; }
        public string Extension { get; set; }
        public int Size { get; set; }
        public byte[] Data { get; set; }
    }

}
