using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VtfHeaderParserLib
{
    public class VtfFile
    {
        public int VersionMajor { get; private set; }
        public int VersionMinor { get; private set; }
        public short LargestMipmapWidth { get; private set; }
        public short LargestMipmapHeight { get; private set; }
        public short AmountOfFrames { get; private set; }
        public short FirstFrame { get; private set; }
        public float BumpmapScale { get; private set; }
        public int HighResolutionImageFormat { get; private set; }
        public int AmountOfMipmaps { get; private set; }

        // This var is unsigned to allow comparison against 0xFFFFFFFF to check a thumbnail exists.
        public uint LowResolutionImageFormat { get; private set; }
        public short LowResolutionImageWidth { get; private set; }
        public short LowResolutionImageHeight { get; private set; }
        public int TextureDepth { get; private set; }
        
        // These vars are kept internal because there doesn't appear to be any need for these externally.
        private int _headerSize;
        private uint _rawFlags;
        private int _numberOfResources;

        private readonly List<KeyValuePair<string, string>> _resourceTags = new List<KeyValuePair<string,string>>()
        {
            new ("\x01\0\0", "Thumbnail"),
            new ("\x30\0\0", "High Res Image"),
            new ("\x10\0\0", "Animated Particle Sheet"),
            new ("CRC", "CRC Data"),
            new ("LOD", "Level of Detail"),
            new ("TSO", "Extended Custom Flags"),
            new ("KVD", "Arbitrary KeyValues")
        };

        private readonly float[] _reflectivity = new float[3];
        public ReadOnlyCollection<float> Reflectivity => Array.AsReadOnly(_reflectivity);
        
        private readonly List<KeyValuePair<string, string>> _keyValuePairs = new();
        public IEnumerable<KeyValuePair<string, string>> KeyValuePairs => _keyValuePairs.AsEnumerable();
        
        private readonly List<string> _tags = new();
        public IEnumerable<string> Tags => _tags.AsEnumerable();
        
        private readonly List<string> _flags = new();
        public IEnumerable<string> Flags => _flags.AsEnumerable();

        public VtfFile(string path)
        {
            ParseHeader(path);
        }
        
        private void ParseFlags(BinaryReader vtfFile)
        {
            _rawFlags = vtfFile.ReadUInt32();
            if (_rawFlags != 0)
            {
                Debug.WriteLine($"This VTF has the following flags: 0x{_rawFlags:x8}");
                foreach (uint currentFlag in Enum.GetValues(typeof(VtfFlags)))
                {
                    if ((currentFlag & _rawFlags) == 0) continue;
                    
                    Debug.WriteLine($"- {(VtfFlags)currentFlag,-30} (0x{currentFlag:x8})");
                    
                    // Couldn't figure out how to do this in one line.
                    var castFlag = (VtfFlags)currentFlag;
                    _flags.Add(castFlag.ToString());
                }
            }
            else
            {
                Debug.WriteLine("No Flags found.");
            }
        }
        
        private void ParseReflectivity(BinaryReader vtfFile)
        {
            for (var i = 0; i < 3; i++)
            {
                _reflectivity[i] = vtfFile.ReadSingle();
            }
            
            Debug.WriteLine($"Reflectivity: {_reflectivity[0]} {_reflectivity[1]} {_reflectivity[2]}");
        }
        
        private void ParseKeyValues(string keyValues)
        {
            // TODO: Can KVs even have quotation marks?
            string[] keyValueSplitChars = {"\n", "\t", "\r", "\"", " ", "{", "}"};
            var splitKeyValues = keyValues.Split(keyValueSplitChars, StringSplitOptions.RemoveEmptyEntries);
            
            // Remove the "Information" part from the array as it's not a KeyValue.
            splitKeyValues = splitKeyValues.Skip(1).ToArray();
            
            var parsingKey = true;
            var entryRepeat = 0;

            string entryKey = null;
            string entryValue = null;
            
            foreach (var currentEntry in splitKeyValues)
            {
                if (parsingKey)
                {
                    Debug.Write($"  - {currentEntry}: ");
                    entryKey = currentEntry;
                    parsingKey = false;
                    entryRepeat++;
                }
                else
                {
                    Debug.WriteLine($"{currentEntry}");
                    entryValue = currentEntry;
                    parsingKey = true;
                    entryRepeat++;
                }

                if (entryRepeat == 2)
                {
                    _keyValuePairs.Add(new KeyValuePair<string, string>(entryKey, entryValue));
                    entryRepeat = 0;
                }
            }
        }
        
        private void ParseDepthAndResources(BinaryReader vtfFile)
        {
            if (VersionMinor < 2) return;
            
            TextureDepth = vtfFile.ReadInt16();
            Debug.WriteLine($"Texture Depth: {TextureDepth}");
            
            if (VersionMinor < 3) return;
            
            // Skip 3 Bytes.
            vtfFile.ReadBytes(3);
            
            _numberOfResources = vtfFile.ReadInt32();
            Debug.WriteLine($"Number of Resources: {_numberOfResources}");
            
            // Skip 8 Bytes.
            vtfFile.ReadBytes(8);
            
            for (var i = 0; i < _numberOfResources; i++)
            {
                var inputTag = Encoding.Default.GetString(vtfFile.ReadBytes(3));

                foreach (var (key, value) in _resourceTags)
                {
                    if (inputTag != key) continue;
                    
                    Debug.WriteLine($"- {value}");
                    _tags.Add(value);
                }

                // Skip Resource flag, it is unused.
                vtfFile.ReadByte();

                // Is this the place to print this information?
                if (inputTag == "LOD")
                {
                    var lodU = vtfFile.ReadByte();
                    var lodV = vtfFile.ReadByte();
                    Debug.WriteLine($"  - Clamp U: {lodU}\n  - Clamp V: {lodV}");
                    
                    // Skip remainder bytes as the LOD values are in 2 bytes, not 4.
                    vtfFile.ReadBytes(2);
                }
                else if (inputTag == "KVD")
                {
                    var resourceOffset = vtfFile.ReadInt32();
                    
                    // Move ahead in the file by the offset given minus the header size, and nudged forward by 4 bytes.
                    vtfFile.ReadBytes((resourceOffset - _headerSize) + 4);

                    string keyValues = null;
                    while (vtfFile.BaseStream.Position != vtfFile.BaseStream.Length)
                    {
                        // 64 Bytes is arbitrary number, better suggestion?
                        keyValues += Encoding.Default.GetString(vtfFile.ReadBytes(64));
                    }
                    
                    ParseKeyValues(keyValues);
                }
                else
                {
                    // Nothing to be used, skip the 4 bytes.
                    vtfFile.ReadBytes(4);
                }
            }
        }

        private void ParseHeader(string path)
        {
            using var vtfFile = new BinaryReader(File.Open(path,FileMode.Open));
            
            Debug.WriteLine($"Opening: {path}");
            
            const string headerSignature = "VTF\0";
            var inputSignature = Encoding.Default.GetString(vtfFile.ReadBytes(4));
            
            if (inputSignature == headerSignature)
            {
                VersionMajor = vtfFile.ReadInt32();
                VersionMinor = vtfFile.ReadInt32();
                Debug.WriteLine($"VTF Version {GUIFileVersion()}");
                
                _headerSize = vtfFile.ReadInt32();
                Debug.WriteLine($"Header Size: {_headerSize} Bytes");
                
                LargestMipmapWidth = vtfFile.ReadInt16();
                LargestMipmapHeight = vtfFile.ReadInt16();
                Debug.WriteLine($"Texture Dimensions: {GUIDimensions()}");
                
                ParseFlags(vtfFile);
                
                AmountOfFrames = vtfFile.ReadInt16();
                Debug.WriteLine($"Amount of Frames: {AmountOfFrames}");
                
                FirstFrame = vtfFile.ReadInt16();
                Debug.WriteLine($"First Frame: {FirstFrame}");
                
                // Skip padding of 4 bytes.
                vtfFile.ReadBytes(4);
                
                ParseReflectivity(vtfFile);
                
                // Skip padding of 4 bytes.
                vtfFile.ReadBytes(4);
                
                BumpmapScale = vtfFile.ReadSingle();
                Debug.WriteLine($"Bumpmap Scale: {BumpmapScale}");
                
                HighResolutionImageFormat = vtfFile.ReadInt32();
                Debug.WriteLine($"Texture Format: {GUIImageFormat()}");
                
                AmountOfMipmaps = vtfFile.ReadByte();
                Debug.WriteLine($"Amount of Mipmaps: {AmountOfMipmaps}");
                
                LowResolutionImageFormat = vtfFile.ReadUInt32();
                Debug.WriteLine($"Thumbnail Format: {GUIThumbnailFormat()}");
                
                LowResolutionImageWidth = vtfFile.ReadByte();
                LowResolutionImageHeight = vtfFile.ReadByte();
                Debug.WriteLine($"Thumbnail Dimensions: {LowResolutionImageWidth} X {LowResolutionImageHeight}");
                
                ParseDepthAndResources(vtfFile);
                
                Debug.WriteLine("");
            }
            else
            {
                throw new ArgumentOutOfRangeException(path, "File is not a valid VTF, File Signature did not match");
            }
        }

        //--------------
        // Misc. Methods
        //--------------

        /// <summary>
        /// Returns if the VTF is a compressed (DXT) Format.
        /// </summary>
        public bool IsCompressedFormat()
        {
            // Entries 13 to 15 in the ImageFormat enum are DXT, the rest aren't compressed.
            return (HighResolutionImageFormat > 12 && HighResolutionImageFormat < 16);
        }
        
        public bool IsAnimated()
        {
            return AmountOfFrames > 1;
        }
        
        public bool HasMipmaps()
        {
            return AmountOfMipmaps != 0;
        }
        
        public bool HasThumbnail()
        {
            return (ImageFormats)LowResolutionImageFormat != ImageFormats.NONE;
        }
        
        public bool HasFlags()
        {
            return _rawFlags != 0;
        }
        
        public bool HasKeyValues()
        {
            return KeyValuePairs.Any();
        }

        //----------------------------------------------------------
        // String Methods mainly intended for GUI usage as a library
        //----------------------------------------------------------

        public string GUIFileVersion()
        {
            return VersionMajor + "." + VersionMinor;
        }

        public string GUIDimensions()
        {
            return LargestMipmapWidth + " X " + LargestMipmapHeight;
        }
        
        public string GUIThumbnailDimensions()
        {
            return LowResolutionImageWidth + " X " + LowResolutionImageHeight;
        }

        public string GUIFrameAmount()
        {
            return IsAnimated() ? AmountOfFrames + " (Animated)" : AmountOfFrames + " (Not Animated)";
        }

        public string GUIReflectivity()
        {
            string reflectivityFormatted = null;
            foreach (var item in Reflectivity)
            {
                reflectivityFormatted += item.ToString() + " ";
            }
            return reflectivityFormatted;
        }

        public string GUIReflectivity(int index)
        {
            return Reflectivity[index].ToString("N5");
        }

        public string GUIImageFormat()
        {
            return ((ImageFormats)HighResolutionImageFormat).ToString();
        }

        public string GUIThumbnailFormat()
        {
            return ((ImageFormats)LowResolutionImageFormat).ToString();
        }
    }
}