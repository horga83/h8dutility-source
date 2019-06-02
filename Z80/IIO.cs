using System;

namespace Z80
{
	/// <summary>
	/// Basic interface for IO access
	/// </summary>
	public interface IIO
	{

		/// <summary>
		/// Read a single byte from a port
		/// </summary>
		/// <param name="Port">Port to read from</param>
		/// <returns>Byte readed</returns>
		byte ReadPort(ushort Port);

		/// <summary>
		/// Write a single byte to a port
		/// </summary>
		/// <param name="Port">Port to write to</param>
		/// <param name="Value">Byte to write</param>
		void WritePort(ushort Port, byte Value);

	
	}

	/// <summary>
	/// Implemented by input devices
	/// </summary>
	public interface IIDevice
	{
		/// <summary>
		/// A port access. When Z80 asks to read from a port
		/// consumers (usually IIO implementers) should asks to every input device (IIDevice implementers)
		/// to read the byte from the specified port. If the result of the function is true, this means
		/// that the IDevice has handled the request so the consumer should not ask to other devices for the
		/// input
		/// </summary>
		/// <param name="Port">Port address</param>
		/// <param name="Value">Value readed</param>
		/// <returns>True if the port is handled by this device, false otherwise</returns>
		bool ReadPort(ushort Port, out byte Value);
	}


	public interface IODevice
	{
		/// <summary>
		/// A port access. When Z80 asks to write to a port
		/// consumers (usually IIO implementers) should asks to every input device (IODevice implementers)
		/// to write the byte to the specified port. If the result of the function is true, this means
		/// that the ODevice has handled the request so the consumer should not ask to other devices for the
		/// output
		/// </summary>
		/// <param name="Port">Port address</param>
		/// <param name="Value">Value to write</param>
		/// <returns>
		/// True if the port is handled by this device, false otherwise.
		/// This is different from input ports because an output port can be handled
		/// by more than one Device
		/// </returns>
		bool WritePort(ushort Port, byte Value);
	}

}
