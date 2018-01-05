namespace miniJson.Stream
{
	interface IReader
	{
		char Read();
		string Read(int count);
		char PeekToBuffer();
		char Current();
		void ClearBuffer();
		string Buffer { get; }
		string BufferPeek { get; }
		string BufferPreLastPeek { get; }
		char Peek();


		long Position { get; }
	}
}