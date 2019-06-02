using System;
using System.Collections;

namespace Z80
{



	/// <summary>
	/// Class for tracing Z80 history. It can keep many Z80 status snapshots
	/// </summary>
	public class Z80HistoryTracer
	{

		public delegate void OnShotAddHandler(Z80Shot Shot);
		public event OnShotAddHandler OnShotAdd;

		private Z80.uP _Z80;
		private Z80.uP.OnFetchHandler FetchHandler;
		private Z80ShotCollection _Z80Shots = new Z80ShotCollection();


		/// <summary>
		/// Creator
		/// </summary>
		public Z80HistoryTracer()
		{
			// Fetch Handler for this instance
			FetchHandler = new Z80.uP.OnFetchHandler(_Z80_OnFetch);
		}

		/// <summary>
		/// Create a new instance. The trace will be based on Z80 specified. During class creation
		/// The event OnFetch is catched to perform automatic snapshots
		/// </summary>
		/// <param name="Z80">Z80 to trace</param>
		public Z80HistoryTracer(uP Z80):this()
		{
			this.Z80 = Z80;
		}

		/// <summary>
		/// The collection of Z80 snapshots
		/// </summary>
		public Z80ShotCollection Z80Shots
		{
			get
			{
				return _Z80Shots;
			}
		}


		/// <summary>
		/// Processor. During set, the event OnFetch is catched to perform automatic snapshots
		/// </summary>
		public Z80.uP Z80
		{
			get
			{
				return _Z80;
			}
			set
			{
				// Check if there is an old fetch handler
				if (_Z80 != null)
					_Z80.OnFetch -= FetchHandler;

				// Change the uP that's going to be monitored
				_Z80 = value;

				if (value != null)
					// Catch the event with the fetch handler
					_Z80.OnFetch += FetchHandler;
			}
		}

		/// <summary>
		/// Raised when a new instruction in performed
		/// </summary>
		private void _Z80_OnFetch()
		{
			Z80Shot z80Shot = _Z80Shots.Add(_Z80);
			if (OnShotAdd != null)
				OnShotAdd(z80Shot);
		}

	
	}




	/// <summary>
	/// Class to contain a single Z80 snapshot
	/// </summary>
	public class Z80Shot
	{
		
		/// <summary>
		/// Creates a new Z80 shot based on uP status and memory specified
		/// </summary>
		/// <param name="Z80">Z80 system to shot</param>
		public Z80Shot(uP Z80)
		{
			_Status = Z80.Status.Clone();
			_SPMemory = new MemoryShot(Z80.Memory, _Status.SP, 2);
			_PCMemory = new MemoryShot(Z80.Memory, _Status.PC, 10);
		}

		private Status _Status;
		private MemoryShot _SPMemory;
		private MemoryShot _PCMemory;

		/// <summary>
		/// Z80 Status shot
		/// </summary>
		public Status Status
		{
			get
			{
				return _Status;
			}
		}


		/// <summary>
		/// Some bytes of memory next to SP shot
		/// </summary>
		public MemoryShot SPMemory
		{
			get
			{
				return _SPMemory;
			}
		}


		/// <summary>
		/// Some bytes of memory next to PC shot
		/// </summary>
		public MemoryShot PCMemory
		{
			get
			{
				return _PCMemory;
			}
		}




		/// <summary>
		/// Contains a snapshot of some bytes of memory
		/// </summary>
		public class MemoryShot : IMemory
		{
			byte[] _Raw;
			ushort _StartAddress;

			/// <summary>
			/// Take a shot of Lenght bytes of memory
			/// </summary>
			/// <param name="Memory">Source Memory</param>
			/// <param name="StartAddress">Address where copy start</param>
			/// <param name="Lenght">Number of bytes to copy</param>
			public MemoryShot(IMemory Memory, ushort StartAddress, byte Lenght)
			{
				_StartAddress = StartAddress;
				_Raw = new byte[Lenght];
				for (int n = 0; n < Lenght; n++)
					_Raw[n] = Memory.ReadByte((ushort) (StartAddress + n));
			}

			/// <summary>
			/// Take a shot of 10 bytes of memory
			/// </summary>
			/// <param name="Memory">Source Memory</param>
			/// <param name="StartAddress">Address where copy will start</param>
			public MemoryShot(IMemory Memory, ushort StartAddress):this(Memory, StartAddress, 10)
			{
			}


			/// <summary>
			/// Get a byte from the snapshot
			/// </summary>
			public byte this[int Address]
			{
				get
				{
					return _Raw[(ushort) (Address - _StartAddress)];
				}
				set
				{
					throw new Exception("MemoryShot.this.set: This should be a snapshot!!!");
				}
			}


			/// <summary>
			/// Never raised, implemented only for compatibility with the interface
			/// </summary>
			public event OnReadHandler OnRead;

			/// <summary>
			/// Never raised, implemented only for compatibility with the interface
			/// </summary>
			public event OnWriteHandler OnWrite;


			/// <summary>
			/// Raw memory (it raises an error because we don't have the whole memory here).
			/// </summary>
			public byte[] Raw
			{
				get
				{
					throw new Exception("MemoryShot.Raw.get: Cannot access to Raw data!");
				}
			}

			/// <summary>
			/// Memory size (it raises an error because we don't have the whole memory here).
			/// </summary>
			public int Size
			{
				get
				{
					throw new Exception("MemoryShot.Size.get: The size of the snapshot is not the size of the memory");
				}
			}

			/// <summary>
			/// Read a single byte from snapshot
			/// </summary>
			/// <param name="Address">Absolute address</param>
			/// <returns>The byte readed</returns>
			public byte ReadByte(ushort Address)
			{
				return this[Address];
			}

			/// <summary>
			/// Read a single word from snapshot
			/// </summary>
			/// <param name="Address">Absolute address</param>
			/// <returns>The word readed</returns>
			public ushort ReadWord(ushort Address)
			{
				return (ushort) (ReadByte((ushort) (Address + 1)) << 8 | ReadByte(Address));
			}


			/// <summary>
			/// Writes a single byte to snapshot
			/// </summary>
			/// <param name="Address">Absolute address</param>
			/// <param name="Value">The value to write</param>
			public void WriteByte(ushort Address, byte Value)
			{
				throw new Exception("Z80History.WriteByte not implemented");
			}

			/// <summary>
			/// Write a single word to snapshot
			/// </summary>
			/// <param name="Address">Absolute address</param>
			/// <param name="Value">The value to write</param>
			public void WriteWord(ushort Address, ushort Value)
			{
				throw new Exception("Z80History.WriteWord not implemented");
			}


		}

	}

	/// <summary>
	/// Contains a set of Z80 shots
	/// </summary>
	public class Z80ShotCollection : IEnumerable
	{
		private ArrayList _InnerCollection = new ArrayList();
		private int _MaxLength = 100;

		/// <summary>
		/// Creates and add a new Z80 snapshot
		/// </summary>
		/// <param name="Z80">Z80 to take a snapshot on</param>
		/// <returns>The just created Z80 snapshot</returns>
		public Z80Shot Add(uP Z80)
		{
			Z80Shot z80Shot = new Z80Shot(Z80);
			_InnerCollection.Add(z80Shot);

			TrimCollection();

			return z80Shot;
		}


		/// <summary>
		/// Trims the collection to the size specified in MaxLength
		/// </summary>
		private void TrimCollection()
		{
			if (_MaxLength < 0)
				return;
				
			while (_InnerCollection.Count > _MaxLength)
				// Delete oldest snapshot
				_InnerCollection.RemoveAt(0);

		}

		/// <summary>
		/// Number of Z80 shots contained in the collection
		/// </summary>
		public int Count
		{
			get
			{
				return _InnerCollection.Count;
			}
		}

			
		/// <summary>
		/// Maximum Z80 shots that this collection will contain (100 by default). Specify a negative number to unlimit the size
		/// </summary>
		public int MaxLength
		{
			get
			{
				return _MaxLength;
			}
			set
			{
				_MaxLength = value;
			}
		}


		/// <summary>
		/// Gets a single shot
		/// </summary>
		public Z80Shot this[int index]
		{
			get
			{
				return (Z80Shot) _InnerCollection[index];
			}
		}


		/// <summary>
		/// Gets an enumerator to iterate the collection
		/// </summary>
		/// <returns>The enumerator</returns>
		public IEnumerator GetEnumerator()
		{
			return _InnerCollection.GetEnumerator();
		}

	}



}
