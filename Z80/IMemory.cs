using System;

namespace Z80
{
	/// <summary>
	/// Basic interface for memory access
	/// </summary>
	public interface IMemory
	{
 
		/// <summary>
		/// OnRead event (called before read)
		/// </summary>
		event OnReadHandler OnRead;
 
		/// <summary>
		/// OnWrite event (called before write)
		/// </summary>
		event OnWriteHandler OnWrite;

		/// <summary>
		/// Read a single byte from memory
		/// </summary>
		/// <param name="Address">Address to read from</param>
		/// <returns>Byte readed</returns>
		byte ReadByte(ushort Address);

		/// <summary>
		/// Write a single byte in memory
		/// </summary>
		/// <param name="Address">Address to write to</param>
		/// <param name="Value">Byte to write</param>
		void WriteByte(ushort Address, byte Value);
	
		/// <summary>
		/// Read a word from memory
		/// </summary>
		/// <param name="Address">Address to read from. Z80 is big endian so bits 0-7 are readed from Address, bits 8-15 are readed from Address + 1</param>
		/// <returns>Word readed</returns>
		ushort ReadWord(ushort Address);

		/// <summary>
		/// Write a word to memory
		/// </summary>
		/// <param name="Address">Address to write to. Z80 is big endian so bits 0-7 are writed to Address, bits 8-15 are writed to Address + 1</param>
		/// <param name="Value">Word to write</param>
		void WriteWord(ushort Address, ushort Value);


		/// <summary>
		/// Total size in byte of memory (include paged memory too)
		/// </summary>
		int Size
		{
			get;
		}

		/// <summary>
		/// Access to plain memory not using any pagination function.
		/// It can be used for cheat search, for direct memory access
		/// and so on
		/// </summary>
		byte this[int Address]
		{
			get;
			set;
		}


		/// <summary>
		/// Byte array containing the whole memory.
		/// It can be used for cheat search, for direct memory access,
		/// for saving and loading images and so on
		/// </summary>
		byte[] Raw
		{
			get;
		}

	}

	/// <summary>
	/// Handler of a memory read event (not a standard M$ event declaration but...)
	/// </summary>
	public delegate void OnReadHandler(ushort Address);

	/// <summary>
	/// Handler of a memory write event (not a standard M$ event declaration but...)
	/// </summary>
	public delegate void OnWriteHandler(ushort Address, byte Value);

}
