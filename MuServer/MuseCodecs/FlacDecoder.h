#pragma once

class OurDecoder;

namespace MuServerCodecs
{
	using namespace System;
	
	public ref class FlacDecoder
	{
	private:	// fields
		OurDecoder *_pDec;

	public:	// constructors
		FlacDecoder(String^ flacFilePath);

		// frees native resources
		!FlacDecoder();

		~FlacDecoder()
		{
			// frees managed resources
			this->!FlacDecoder();
		}

	public:	// Properties

		property int TotalBytes
		{
			int get();
		}

	public:	// Methods
		int GetData(array<byte>^ buffer);

		void Stop();

		bool EndOfStream();

	protected:
		static void DecoderProc(Object^ obj);

	};
}
