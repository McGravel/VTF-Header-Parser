using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VtfHeaderParser
{
    public class VtfHeader
    {
        // TODO: Determine which of these variables are used and should be kept.
        // This is otherwise somewhat wasteful to have many variables only used in printing text.
        // For example, the major version is only used in printing, so is this variable necessary?
        private int     _versionMajor;
        // Whereas the minor version is used to parse further into the header, as later versions have more to parse.
        private int     _versionMinor;
        
        private int     _headerSize;
        
        private short   _largestMipmapWidth;
        private short   _largestMipmapHeight;
        
        private uint    _flags;
        
        private short   _amountOfFrames;
        private short   _firstFrame;
        
        private readonly float[] _reflectivityVector = new float[3];
        
        private float   _bumpmapScale;
        private int     _highResolutionImageFormat;
        private int     _amountOfMipmaps;
        
        // This var is unsigned to allow comparison against 0xFFFFFFFF to check a thumbnail exists.
        private uint    _lowResolutionImageFormat;
        private short   _lowResolutionImageWidth;
        private short   _lowResolutionImageHeight;
        
        private int     _textureDepth;
        private int     _numberOfResources;

        private readonly List<KeyValuePair<string,string>> _resourceTags = new List<KeyValuePair<string,string>>()
        {
            new ("\x01\0\0", "Thumbnail"),
            new ("\x30\0\0", "High Res Image"),
            new ("\x10\0\0", "Animated Particle Sheet"),
            new ("CRC", "CRC Data"),
            new ("LOD", "Level of Detail"),
            new ("TSO", "Extended Custom Flags"),
            new ("KVD", "Arbitrary KeyValues")
        };
        
        public VtfHeader(string path)
        {
            ParseHeader(path);
        }
        
        private void ParseFlags(BinaryReader vtfFile)
        {
            _flags = vtfFile.ReadUInt32();
            if (_flags != 0)
            {
                Console.WriteLine($"This VTF has the following flags: 0x{_flags:x8}");
                foreach (uint currentFlag in Enum.GetValues(typeof(VtfFlags)))
                {
                    if ((currentFlag & _flags) != 0)
                    {
                        Console.WriteLine($"* {(VtfFlags)currentFlag,-30} (0x{currentFlag:x8})");
                    }
                }
            }
            else
            {
                Console.WriteLine("No Flags found.");
            }
        }
        
        private void ParseReflectivity(BinaryReader vtfFile)
        {
            for (var i = 0; i < 3; i++)
            {
                _reflectivityVector[i] = vtfFile.ReadSingle();
            }
            
            Console.WriteLine($"Reflectivity: {_reflectivityVector[0]} {_reflectivityVector[1]} {_reflectivityVector[2]}");
        }
        
        private void ParseDepthAndResources(BinaryReader vtfFile)
        {
            _lowResolutionImageFormat = vtfFile.ReadUInt32();
            Console.WriteLine($"Thumbnail Format: {(ImageFormats)_lowResolutionImageFormat}");
            
            _lowResolutionImageWidth = vtfFile.ReadByte();
            _lowResolutionImageHeight = vtfFile.ReadByte();
            Console.WriteLine($"Thumbnail Dimensions: {_lowResolutionImageWidth} X {_lowResolutionImageHeight}");
            
            if (_versionMinor < 2) return;
            
            _textureDepth = vtfFile.ReadInt16();
            Console.WriteLine($"Texture Depth: {_textureDepth}");
            
            if (_versionMinor < 3) return;
            
            // Skip 3 Bytes.
            vtfFile.ReadBytes(3);
            
            _numberOfResources = vtfFile.ReadInt32();
            Console.WriteLine($"Number of Resources: {_numberOfResources}");
            
            // Skip 8 Bytes.
            vtfFile.ReadBytes(8);
            
            for (var i = 0; i < _numberOfResources; i++)
            {
                var inputTag = Encoding.Default.GetString(vtfFile.ReadBytes(3));

                foreach (var (key, value) in _resourceTags)
                {
                    if (inputTag == key)
                    {
                        Console.WriteLine($"* {value}");
                    }
                }

                // Skip Resource flag, it is unused.
                vtfFile.ReadByte();

                // Is this the place to print this information?
                if (inputTag == "LOD")
                {
                    int lodU = vtfFile.ReadByte();
                    int lodV = vtfFile.ReadByte();
                    Console.WriteLine($"Level of Detail Tag:\n* Clamp U: {lodU}\n* Clamp V: {lodV}");
                    
                    // Skip remainder bytes as the LOD values are in 2 bytes, not 4.
                    vtfFile.ReadBytes(2);
                }
                else
                {
                    // TODO: Store offset and parse the Tag's actual data later?
                    var resourceOffset = vtfFile.ReadInt32();
                }
            }
        }
        
        private void ParseHeader(string path)
        {
            using var vtfFile = new BinaryReader(File.Open(path,FileMode.Open));
            
            Console.WriteLine($"Opening: {path}");
            
            const string headerSignature = "VTF\0";
            var inputSignature = Encoding.Default.GetString(vtfFile.ReadBytes(4));
            
            if (inputSignature == headerSignature)
            {
                _versionMajor = vtfFile.ReadInt32();
                _versionMinor = vtfFile.ReadInt32();
                Console.WriteLine($"VTF Version {_versionMajor}.{_versionMinor}");
                
                _headerSize = vtfFile.ReadInt32();
                Console.WriteLine($"Header Size: {_headerSize} Bytes");
                
                _largestMipmapWidth = vtfFile.ReadInt16();
                _largestMipmapHeight = vtfFile.ReadInt16();
                Console.WriteLine($"Texture Dimensions: {_largestMipmapWidth} X {_largestMipmapHeight}");
                
                ParseFlags(vtfFile);
                
                _amountOfFrames = vtfFile.ReadInt16();
                Console.WriteLine($"Amount of Frames: {_amountOfFrames}");
                
                _firstFrame = vtfFile.ReadInt16();
                Console.WriteLine($"First Frame: {_firstFrame}");
                
                // Skip padding of 4 bytes.
                vtfFile.ReadBytes(4);
                
                ParseReflectivity(vtfFile);
                
                // Skip padding of 4 bytes.
                vtfFile.ReadBytes(4);
                
                _bumpmapScale = vtfFile.ReadSingle();
                Console.WriteLine($"Bumpmap Scale: {_bumpmapScale}");
                
                _highResolutionImageFormat = vtfFile.ReadInt32();
                Console.WriteLine($"Texture Format: {(ImageFormats)_highResolutionImageFormat}");
                
                _amountOfMipmaps = vtfFile.ReadByte();
                Console.WriteLine($"Amount of Mipmaps: {_amountOfMipmaps}");
                
                ParseDepthAndResources(vtfFile);
                
                Console.WriteLine("");
            }
            else
            {
                throw new ArgumentOutOfRangeException(path, "File is not a valid VTF, File Signature did not match");
            }
        }
    }
}