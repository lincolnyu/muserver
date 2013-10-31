#include <stdlib.h>
#include <string>
#include <Windows.h>
#include <tchar.h>
#include "share\compat.h"
#include "FLAC++\decoder.h"

#include "FlacDecoder.h"


class FlacDecodingException
{
private:
	std::string _message;

public:
	FlacDecodingException(const std::string &message)
		: _message(message)
	{
	}

public:
	const std::string & GetMessage()
	{
		return _message;
	}
};


class OurDecoder: public FLAC::Decoder::File 
{
private:	// fields
	FLAC__uint64 _totalSamples;
	unsigned _sampleRate;
	unsigned _channels;
	unsigned _bps;

	int _bufferSize;
	unsigned char *_buffer;
	int _bytesWritten;
	int _bytesRead;
	bool _finishedWriting;
	int _totalBytes;

	HANDLE _hReadEvent;
	HANDLE _hWriteEvent;

	bool _cancelled;

public:
	OurDecoder(int bufferSize = 4*1024*1024);
	virtual ~OurDecoder();

public:	// Properties

	// number of total bytes including header (based on meta-data rather than _totalBytes)
	int TotalBytes();

public:	// Methods

	int SizeForWriting();
	int SizeToRead();

	// it's the caller that ensures the readibility of the specified 
	// number of bytes
	void Read(unsigned char *buf, int size);

	void Stop();

	bool EndOfStream();
	void SetEndOfStream();
	bool FinishedWriting();

	void GetReadEvent();
	void GetWriteEvent();

protected:

	virtual ::FLAC__StreamDecoderWriteStatus write_callback(const ::FLAC__Frame *frame, const FLAC__int32 * const buffer[]);
	virtual void metadata_callback(const ::FLAC__StreamMetadata *metadata);
	virtual void error_callback(::FLAC__StreamDecoderErrorStatus status);

	void PutReadEvent();
	void PutWriteEvent();

	void WriteBytes(void *source, int numBytes);
	void WriteLittleEndianUint16(FLAC__uint16 x);
	void WriteLittleEndianInt16(FLAC__int16 x);
	void WriteLittleEndianUint32(FLAC__uint32 x);

private:
	OurDecoder(const OurDecoder&);
	OurDecoder&operator=(const OurDecoder&);
};

OurDecoder::OurDecoder(int bufferSize) 
	: FLAC::Decoder::File()
	, _totalSamples(0), _sampleRate(0), _channels(0), _bps(0)
	, _bufferSize(bufferSize), _buffer(new unsigned char[bufferSize])
	, _bytesRead(0), _bytesWritten(0), _finishedWriting(false), _totalBytes(0)
	, _cancelled(false)
{
	_hReadEvent = ::CreateEvent(NULL, FALSE, FALSE, _T("Read Event"));
	if (_hReadEvent == 0)
	{
		throw FlacDecodingException("Error creating read event");
	}
	_hWriteEvent = ::CreateEvent(NULL, FALSE, FALSE, _T("Write Event"));
	if (_hWriteEvent == 0)
	{
		throw FlacDecodingException("Error creating write event");
	}
}

OurDecoder::~OurDecoder()
{
	if (_hReadEvent)
	{
		::CloseHandle(_hReadEvent);
	}
	if (_hWriteEvent)
	{
		::CloseHandle(_hWriteEvent);
	}
	delete[] _buffer;
}


static bool write_little_endian_uint16(FILE *f, FLAC__uint16 x)
{
	return
		fputc(x, f) != EOF &&
		fputc(x >> 8, f) != EOF
	;
}
static bool write_little_endian_int16(FILE *f, FLAC__int16 x)
{
	return write_little_endian_uint16(f, (FLAC__uint16)x);
}
static bool write_little_endian_uint32(FILE *f, FLAC__uint32 x)
{
	return
		fputc(x, f) != EOF &&
		fputc(x >> 8, f) != EOF &&
		fputc(x >> 16, f) != EOF &&
		fputc(x >> 24, f) != EOF
	;
}

::FLAC__StreamDecoderWriteStatus OurDecoder::write_callback(const ::FLAC__Frame *frame, const FLAC__int32 * const buffer[])
{
	if (_cancelled)
	{
		return ::FLAC__StreamDecoderWriteStatus::FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
	}

	const FLAC__uint32 totalSize = (FLAC__uint32)(_totalSamples * _channels * (_bps/8));
	size_t i;

	if (_totalSamples == 0) 
	{
		//fprintf(stderr, "ERROR: this example only works for FLAC files that have a _totalSamples count in STREAMINFO\n");
		return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
	}
	if (_channels != 2 || _bps != 16) 
	{
		//fprintf(stderr, "ERROR: this example only supports 16bit stereo streams\n");
		return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
	}

	int sizeToWrite = (frame->header.number.sample_number == 0) ? 44 : 0;
	sizeToWrite += frame->header.blocksize*4;

	while (sizeToWrite > SizeForWriting())
	{
		GetWriteEvent();
		if (_cancelled)
		{
			return ::FLAC__StreamDecoderWriteStatus::FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
		}
	}

	unsigned char *pBuf = _buffer + (_bytesWritten%_bufferSize);

	/* write WAVE header before we write the first frame */
	if (frame->header.number.sample_number == 0) 
	{
		// write header (44 bytes)

		WriteBytes("RIFF", 4);
		WriteLittleEndianUint32(totalSize + 36);
		WriteBytes("WAVEfmt ", 8);
		WriteLittleEndianUint32(16);
		WriteLittleEndianUint16(1);
		WriteLittleEndianUint16((FLAC__uint16)_channels);
		WriteLittleEndianUint32(_sampleRate);
		WriteLittleEndianUint32(_sampleRate * _channels * (_bps/8));
		WriteLittleEndianUint16((FLAC__uint16)(_channels * (_bps/8)));		/* block align */
		WriteLittleEndianUint16((FLAC__uint16)_bps);
		WriteBytes("data", 4);
		WriteLittleEndianUint32(totalSize);
	}

	/* write decoded PCM samples (blocksize * 4 bytes) */
	for(i = 0; i < frame->header.blocksize; i++) 
	{
		WriteLittleEndianInt16((FLAC__int16)buffer[0][i]);
		WriteLittleEndianInt16((FLAC__int16)buffer[1][i]);
	}

	PutReadEvent();	

	return FLAC__STREAM_DECODER_WRITE_STATUS_CONTINUE;
}

void OurDecoder::metadata_callback(const ::FLAC__StreamMetadata *metadata)
{
	/* print some stats */
	if(metadata->type == FLAC__METADATA_TYPE_STREAMINFO) 
	{
		/* save for later */
		_totalSamples = metadata->data.stream_info.total_samples;	// total number of samples
		_sampleRate = metadata->data.stream_info.sample_rate;		// sample rate
		_channels = metadata->data.stream_info.channels;			// number of channels
		_bps = metadata->data.stream_info.bits_per_sample;			// bits per sample
	}
}

int OurDecoder::TotalBytes()
{
	return _totalSamples * _channels * (_bps/8) + 44;
}

void OurDecoder::error_callback(::FLAC__StreamDecoderErrorStatus status)
{
	// TODO if error needs to be reported
}

int OurDecoder::SizeForWriting()
{
	int diff = _bytesWritten - _bytesRead;
	return _bufferSize - diff;
}

int OurDecoder::SizeToRead()
{
	int diff = _bytesWritten - _bytesRead;
	return diff;
}

bool OurDecoder::EndOfStream()
{
	return _finishedWriting && (_bytesRead >= _totalBytes);
}

bool OurDecoder::FinishedWriting()
{
	return _finishedWriting;
}

void OurDecoder::SetEndOfStream()
{
	_totalBytes = _bytesWritten;
	_finishedWriting = true;
	PutReadEvent();
}

void OurDecoder::Read(unsigned char *buf, int size)
{
	unsigned char *pBuf = _buffer + (_bytesRead%_bufferSize);
	unsigned char *pBufEnd = _buffer + _bufferSize;
	
	int sizeToEnd = pBufEnd - pBuf;
	if (size < sizeToEnd)
	{
		memcpy(buf, pBuf, size);
		_bytesRead += size;
		pBuf += size;
		size = 0;
		sizeToEnd -= size;
	}
	else
	{
		memcpy(buf, pBuf, sizeToEnd);
		size -= sizeToEnd;
		_bytesRead += sizeToEnd;
	}

	if (size > 0)
	{
		memcpy(buf, _buffer, size);
		_bytesRead += size;
	}

	PutWriteEvent();
}

void OurDecoder::Stop()
{
	_cancelled = true;
	PutWriteEvent();
}

void OurDecoder::GetReadEvent()
{
	::WaitForSingleObject(_hReadEvent, INFINITE);
}

void OurDecoder::GetWriteEvent()
{
	::WaitForSingleObject(_hWriteEvent, INFINITE);
}
	
void OurDecoder::PutReadEvent()
{
	if (!::SetEvent(_hReadEvent))
	{
		throw new FlacDecodingException("Set-read-event failed");
	}
}

void OurDecoder::PutWriteEvent()
{
	if (!::SetEvent(_hWriteEvent))
	{
		throw new FlacDecodingException("Set-write-event failed");
	}
}

void OurDecoder::WriteBytes(void *source, int numBytes)
{
	unsigned char *pSrc = (unsigned char*)source;
	unsigned char *pDst = _buffer+(_bytesWritten%_bufferSize);
	unsigned char *pDstEnd = _buffer+_bufferSize;
	unsigned char *pSrcEnd = pSrc + numBytes;
	for ( ; pSrc != pSrcEnd; )
	{
		*pDst++ = *pSrc++;
		if (pDst == pDstEnd) 
		{
			pDst = _buffer;
		}
	}
	_bytesWritten += numBytes;
}

void OurDecoder::WriteLittleEndianUint16(FLAC__uint16 x)
{
	unsigned char *pDst = _buffer+(_bytesWritten%_bufferSize);
	unsigned char *pDstEnd = _buffer+_bufferSize;
	*pDst++ = x & 0xff;
	if (pDst == pDstEnd)
	{
		pDst = _buffer;
	}
	*pDst++ = (x>>8)&0xff;
	if (pDst == pDstEnd)
	{
		pDst = _buffer;
	}
	_bytesWritten += 2;
}

void OurDecoder::WriteLittleEndianInt16(FLAC__int16 x)
{
	WriteLittleEndianUint16((FLAC__uint16)x);
}

void OurDecoder::WriteLittleEndianUint32(FLAC__uint32 x)
{
	unsigned char *pDst = _buffer+(_bytesWritten%_bufferSize);
	unsigned char *pDstEnd = _buffer+_bufferSize;
	if (pDstEnd - pDst > 4)
	{
		*pDst++ = x & 0xff;
		*pDst++ = (x>>8)&0xff;
		*pDst++ = (x>>16)&0xff;
		*pDst++ = (x>>24)&0xff;
	}
	else
	{
		*pDst++ = x & 0xff;
		if (pDst == pDstEnd)
		{
			pDst = _buffer;
		}
		*pDst++ = (x>>8)&0xff;
		if (pDst == pDstEnd)
		{
			pDst = _buffer;
		}
		*pDst++ = (x>>16)&0xff;
		if (pDst == pDstEnd)
		{
			pDst = _buffer;
		}
		*pDst++ = (x>>24)&0xff;
		if (pDst == pDstEnd)
		{
			pDst = _buffer;
		}
	}
	_bytesWritten += 4;
}

using namespace MuServerCodecs;
using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Threading;

FlacDecoder::FlacDecoder(String^ flacFilePath)
{
	System::IntPtr ptr = Marshal::StringToHGlobalUni(flacFilePath);
	wchar_t *wch = (wchar_t *)ptr.ToPointer();
	
	size_t convertedChars = 0;
	size_t sizeInBytes = (flacFilePath->Length+1)*2;
	char *ch = (char*)malloc(sizeInBytes);
	wcstombs_s(&convertedChars, ch, sizeInBytes, wch, sizeInBytes);
	
	_pDec = new OurDecoder();
	_pDec->init(ch);

	ThreadPool::QueueUserWorkItem(gcnew WaitCallback(DecoderProc), this);
}

FlacDecoder::!FlacDecoder()
{
	Stop();	// always stop it upon finalisation
	// wait for DecoderProc() to finish
	while (!_pDec->FinishedWriting())
	{
		_pDec->GetReadEvent();
	}
	delete _pDec;
}

int FlacDecoder::TotalBytes::get()
{
	return _pDec->TotalBytes();
}

int FlacDecoder::GetData(array<byte>^ buffer)
{
	const int minSizeToRead = 1024;

	int bufLen = buffer->Length;
	int bufWrPt = 0;

	while (bufWrPt < bufLen && !_pDec->EndOfStream())
	{
		while (_pDec->SizeToRead() < minSizeToRead && !_pDec->FinishedWriting())
		{
			_pDec->GetReadEvent();
		}

		int size = _pDec->SizeToRead();
		int bufLeft = buffer->Length - bufWrPt;
		if (size > bufLeft)
		{
			size = bufLeft;
		}

		unsigned char *tempBuf = new unsigned char[size];
		_pDec->Read(tempBuf, size);

		for (int i = 0; i < size; i++, bufWrPt++)
		{
			buffer[bufWrPt] = tempBuf[i];
		}
	}

	return bufWrPt;
}

void FlacDecoder::Stop()
{
	_pDec->Stop();
}

bool FlacDecoder::EndOfStream()
{
	return _pDec->EndOfStream();
}

void FlacDecoder::DecoderProc(Object^ obj)
{
	FlacDecoder^ decoder = (FlacDecoder^)obj;
	OurDecoder *pDec = decoder->_pDec;
	pDec->process_until_end_of_stream();
	pDec->SetEndOfStream();
}

