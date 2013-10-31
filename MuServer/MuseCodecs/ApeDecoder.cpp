#include <windows.h>
#include <malloc.h>
#include "All.h"
#include "MACLib.h"

#include "ApeDecoder.h"

using namespace System::Runtime::InteropServices;
using namespace MuServerCodecs;
using namespace APE;

ApeDecoder::ApeDecoder(String^ apeFilePath) : _recentError(Error::Success)
{
	System::IntPtr ptr = Marshal::StringToHGlobalUni(apeFilePath);
	wchar_t *pszApeFilePath = (wchar_t *)ptr.ToPointer();
	int errorCode;
	_pApeDecompressor = CreateIAPEDecompress(pszApeFilePath, &errorCode);
	if (errorCode != ERROR_SUCCESS)
	{
		_pApeDecompressor = NULL;
		String^ message = String::Format("Error creating APE decoder, error code: {0}", errorCode);
		throw gcnew System::Exception(message);
	}
	_bytesPerBlock = GetInfo(Field::BlockAlign);
}

ApeDecoder::!ApeDecoder()
{
	if (_pApeDecompressor != NULL)
	{
		delete _pApeDecompressor;
		_pApeDecompressor = NULL;
	}
}

int ApeDecoder::GetData(array<byte>^ buffer)
{
	int bufferSize = buffer->Length;
	int numBlocks = bufferSize/_bytesPerBlock;
	int numBlocksRetrieved;
	char *pInnerBuffer = new char[numBlocks*_bytesPerBlock];
	_recentError = (Error)_pApeDecompressor->GetData(pInnerBuffer, numBlocks, &numBlocksRetrieved);
	if (_recentError != Error::Success)
	{
		numBlocksRetrieved = 0;
	}
	int numBytes = numBlocksRetrieved*_bytesPerBlock;
	for (int i = 0; i < numBytes; i++)
	{
		buffer[i] = pInnerBuffer[i];
	}
	delete[] pInnerBuffer;
	return numBytes;
}

int ApeDecoder::GetInfo(Field field)
{
	if (field == Field::WavHeaderData)
	{
		throw gcnew System::Exception("Cannot get wave header data using GetInfo(), use GetHeaderInfo() instead");		
	}
	else
	{
		return _pApeDecompressor->GetInfo((APE_DECOMPRESS_FIELDS)field, 0, 0);
	}
}

ApeDecoder::WavHeader^ ApeDecoder::GetHeaderInfo()
{
	WAVE_HEADER wavHeaderSt;
	_pApeDecompressor->GetInfo((APE_DECOMPRESS_FIELDS)Field::WavHeaderData, (intn)&wavHeaderSt, 0);
	WavHeader^ wavHeader = gcnew WavHeader();

	for (int i = 0; i < 4; i++)
	{
		wavHeader->RiffHeader[i] = wavHeaderSt.cRIFFHeader[i];
		wavHeader->DataTypeID[i] = wavHeaderSt.cDataTypeID[i];
		wavHeader->DataHeader[i] = wavHeaderSt.cDataHeader[i];
	}
	wavHeader->RIFFBytes = wavHeaderSt.nRIFFBytes;
	wavHeader->FormatBytes = wavHeaderSt.nFormatBytes;
	wavHeader->FormatTag = wavHeaderSt.nFormatTag;
	wavHeader->Channels = wavHeaderSt.nChannels;
	wavHeader->SamplesPerSec = wavHeaderSt.nSamplesPerSec;
	wavHeader->AvgBytesPerSec = wavHeaderSt.nAvgBytesPerSec;
	wavHeader->BlockAlign = wavHeaderSt.nBlockAlign;
	wavHeader->BitsPerSample = wavHeaderSt.nBitsPerSample;
	wavHeader->DataBytes = wavHeaderSt.nDataBytes;

	return wavHeader;
}

array<byte>^ ApeDecoder::GetHeaderBytes()
{
	WAVE_HEADER wavHeaderSt;
	_pApeDecompressor->GetInfo((APE_DECOMPRESS_FIELDS)Field::WavHeaderData, (intn)&wavHeaderSt, sizeof(wavHeaderSt));
	array<byte>^ headerBytes = gcnew array<byte>(sizeof(wavHeaderSt));
	unsigned char *pWavHeader = (unsigned char *)&wavHeaderSt;
	for (int i = 0; i < sizeof(wavHeaderSt); i++)
	{
		headerBytes[i] = pWavHeader[i];
	}
	return headerBytes;
}

ApeDecoder::Error ApeDecoder::GetRecentError()
{
	return _recentError;
}

