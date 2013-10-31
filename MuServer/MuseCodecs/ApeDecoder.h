#pragma once

#include "All.h"
#include "MACLib.h"

namespace MuServerCodecs
{
	using namespace System;
	using namespace System::IO;
	using namespace APE;

	public ref class ApeDecoder
	{
	public:	// enumerations
		enum class Error
		{
			Success = ERROR_SUCCESS,
			ErrorIoRead = ERROR_IO_READ, 
			ErrorIoWrite = ERROR_IO_WRITE, 
			InvalidInputFile = ERROR_INVALID_INPUT_FILE, 
			InvalidOutputFile = ERROR_INVALID_OUTPUT_FILE, 
			InputFileTooLarge = ERROR_INPUT_FILE_TOO_LARGE, 
			InputFileUnsupportedBitDepth = ERROR_INPUT_FILE_UNSUPPORTED_BIT_DEPTH, 
			InputFileUnsupportedSampleRate = ERROR_INPUT_FILE_UNSUPPORTED_SAMPLE_RATE, 
			InputFileUnsupportedChannelCount = ERROR_INPUT_FILE_UNSUPPORTED_CHANNEL_COUNT, 
			InputFileTooSmall = ERROR_INPUT_FILE_TOO_SMALL, 
			InvalidChecksum = ERROR_INVALID_CHECKSUM, 
			ErrorCompressingFrame = ERROR_DECOMPRESSING_FRAME, 
			ErrorInitializingUnMac = ERROR_INITIALIZING_UNMAC, 
			InvalidFunctionParameter = ERROR_INVALID_FUNCTION_PARAMETER, 
			UnsupportedFileType = ERROR_UNSUPPORTED_FILE_TYPE, 
			UnsupportedFileVersion = ERROR_UPSUPPORTED_FILE_VERSION, 
			InsufficientMemory = ERROR_INSUFFICIENT_MEMORY, 
			ErrorLoadingApeDll = ERROR_LOADINGAPE_DLL, 
		    ErrorLoadingApeInfoDll = ERROR_LOADINGAPE_INFO_DLL,  
			ErrorLaodingUnMacDll = ERROR_LOADING_UNMAC_DLL,   
			UserStoppedProcessing = ERROR_USER_STOPPED_PROCESSING, 
			Skipped = ERROR_SKIPPED, 
			BadParameter = ERROR_BAD_PARAMETER, 
			ApeCompressTooMuchData = ERROR_APE_COMPRESS_TOO_MUCH_DATA, 
			Undefined = ERROR_UNDEFINED
		};

		enum class Field
		{
			FileVersion = APE_INFO_FILE_VERSION, 
			CompressionLevel = APE_INFO_COMPRESSION_LEVEL,
			FormatFlags = APE_INFO_FORMAT_FLAGS,
			SampleRate = APE_INFO_SAMPLE_RATE,
			BitsPerSample = APE_INFO_BITS_PER_SAMPLE,
			BytesPerSample = APE_INFO_BYTES_PER_SAMPLE,
			Channels = APE_INFO_CHANNELS,
			BlockAlign = APE_INFO_BLOCK_ALIGN,
			BlocksPerFrame = APE_INFO_BLOCKS_PER_FRAME,
			FinalFrameBlocks = APE_INFO_FINAL_FRAME_BLOCKS,
			TotalFrames = APE_INFO_TOTAL_FRAMES,
			WavHeaderBytes = APE_INFO_WAV_HEADER_BYTES,
			WavTerminatingBytes = APE_INFO_WAV_TERMINATING_BYTES,
			WavDataBytes = APE_INFO_WAV_DATA_BYTES,
			WavTotalBytes = APE_INFO_WAV_TOTAL_BYTES,
			ApeTotalBytes = APE_INFO_APE_TOTAL_BYTES,
			TotalBlocks = APE_INFO_TOTAL_BLOCKS,
			LengthMs = APE_INFO_LENGTH_MS,
			AverageBitrate = APE_INFO_AVERAGE_BITRATE,
			FrameBitrate = APE_INFO_FRAME_BITRATE,
			DecompressedBitrate = APE_INFO_DECOMPRESSED_BITRATE,
			PeakLevel = APE_INFO_PEAK_LEVEL,
			SeekBit = APE_INFO_SEEK_BIT,
			SeekByte = APE_INFO_SEEK_BYTE,
			WavHeaderData = APE_INFO_WAV_HEADER_DATA,
			WavTerminatingData = APE_INFO_WAV_TERMINATING_DATA,
			WaveFormatEx = APE_INFO_WAVEFORMATEX,
			IoSource = APE_INFO_IO_SOURCE,
			FrameBytes = APE_INFO_FRAME_BYTES,
			FrameBlocks = APE_INFO_FRAME_BLOCKS,
			Tag = APE_INFO_TAG,
			DecompressCurrentBlock = APE_DECOMPRESS_CURRENT_BLOCK,
			DecompressCurrentMs = APE_DECOMPRESS_CURRENT_MS,
			DecompressTotalBlocks = APE_DECOMPRESS_TOTAL_BLOCKS,
			DecompressLengthMs = APE_DECOMPRESS_LENGTH_MS,
			DecompressCurrentBitrate = APE_DECOMPRESS_CURRENT_BITRATE,
			DecompressAverageBitrate = APE_DECOMPRESS_AVERAGE_BITRATE,
			DecompressCurrentFrame = APE_DECOMPRESS_CURRENT_FRAME,
			InternalInfo = APE_INTERNAL_INFO
		};

	public:	// nested types
		ref class WavHeader
		{
		public:	// fields
			 // RIFF header
			array<char>^ RiffHeader;
			unsigned int RIFFBytes;

			// data type
			array<char>^ DataTypeID;

			// wave format
			array<char>^ FormatHeader;
			unsigned int FormatBytes;

			unsigned short FormatTag;
			unsigned short Channels;
			unsigned int SamplesPerSec;
			unsigned int AvgBytesPerSec;
			unsigned short BlockAlign;
			unsigned short BitsPerSample;
    
			// data chunk header
			array<char>^ DataHeader;
			unsigned int DataBytes;

		public:	// constructors
			WavHeader()
			{
				RiffHeader = gcnew array<char>(4);
				DataTypeID = gcnew array<char>(4);
				FormatHeader = gcnew array<char>(4);
				DataHeader = gcnew array<char>(4);
			}
		};

	private:	// fields
		APE::IAPEDecompress *_pApeDecompressor;
		int _bytesPerBlock;
		Error _recentError;

	public:	// constructors
		ApeDecoder(String^ apeFilePath);
		
		// frees native resources
		!ApeDecoder();

		~ApeDecoder()
		{
			// frees managed resources
			this->!ApeDecoder();
		}

	public:	// methods
		int GetData(array<byte>^ buffer);
		int GetInfo(Field field);
		WavHeader^ GetHeaderInfo();
		array<byte>^ GetHeaderBytes();

		Error GetRecentError();

	};
}
