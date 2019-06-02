using System;

namespace Z80
{
	/// <summary>
	/// Z80 Status
	/// </summary>
	public class Status
	{


		private Register _RegisterAF = new Register();
		private Register _RegisterBC = new Register();
		private Register _RegisterDE = new Register();
		private Register _RegisterHL = new Register();
		private Register _RegisterAF_ = new Register();
		private Register _RegisterBC_ = new Register();
		private Register _RegisterDE_ = new Register();
		private Register _RegisterHL_ = new Register();

		private Register _RegisterIX = new Register();
		private Register _RegisterIY = new Register();

		private byte _I;
		private byte _R;
		private byte _R7;

		private ushort _PC;
		private Register _RegisterSP = new Register();

		private bool _Halted;
		private bool _IFF1;
		private bool _IFF2;
		private byte _IM;


		#region Register AF

		/// <summary>
		/// Accumulator and flags register
		/// </summary>
		public Register RegisterAF
		{
			get
			{
				return _RegisterAF;
			}
			set
			{
				_RegisterAF = value;
			}
		}

		/// <summary>
		/// Access to AF register as word
		/// </summary>
		public ushort AF
		{
			get
			{
				return _RegisterAF.w;
			}
			set
			{
				_RegisterAF.w = value;
			}
		}

		/// <summary>
		/// Access to bits 8-15 of AF
		/// </summary>
		public byte A
		{
			get
			{
				return _RegisterAF.h.Value;
			}
			set
			{
				_RegisterAF.h.Value = value;
			}
		}

		/// <summary>
		/// Access to bits 0-7 of AF
		/// </summary>
		public byte F
		{
			get
			{
				return _RegisterAF.l.Value;
			}
			set
			{
				_RegisterAF.l.Value = value;
			}
		}


		#endregion


		#region Register BC

		/// <summary>
		/// BC Register
		/// </summary>
		public Register RegisterBC
		{
			get
			{
				return _RegisterBC;
			}
			set
			{
				_RegisterBC = value;
			}
		}


		/// <summary>
		/// Access to BC register as word
		/// </summary>
		public ushort BC
		{
			get
			{
				return _RegisterBC.w;
			}
			set
			{
				_RegisterBC.w = value;
			}
		}

		/// <summary>
		/// Access to 8-15 bits of BC
		/// </summary>
		public byte B
		{
			get
			{
				return _RegisterBC.h.Value;
			}
			set
			{
				_RegisterBC.h.Value = value;
			}
		}

		/// <summary>
		/// Access to 0-7 bits of BC
		/// </summary>
		public byte C
		{
			get
			{
				return _RegisterBC.l.Value;
			}
			set
			{
				_RegisterBC.l.Value = value;
			}
		}


		#endregion


		#region Register DE

		/// <summary>
		/// DE Register
		/// </summary>
		public Register RegisterDE
		{
			get
			{
				return _RegisterDE;
			}
			set
			{
				_RegisterDE = value;
			}
		}


		/// <summary>
		/// Access to DE Register as word
		/// </summary>
		public ushort DE
		{
			get
			{
				return _RegisterDE.w;
			}
			set
			{
				_RegisterDE.w = value;
			}
		}

		/// <summary>
		/// Access to bit 8-15 of DE
		/// </summary>
		public byte D
		{
			get
			{
				return _RegisterDE.h.Value;
			}
			set
			{
				_RegisterDE.h.Value = value;
			}
		}

		/// <summary>
		/// Access to bit 0-7 of DE
		/// </summary>
		public byte E
		{
			get
			{
				return _RegisterDE.l.Value;
			}
			set
			{
				_RegisterDE.l.Value = value;
			}
		}


		#endregion


		#region Register HL
		
		/// <summary>
		/// HL Register
		/// </summary>
		public Register RegisterHL
		{
			get
			{
				return _RegisterHL;
			}
			set
			{
				_RegisterHL = value;
			}
		}


		

		/// <summary>
		/// Access to HL register as word
		/// </summary>
		public ushort HL
		{
			get
			{
				return _RegisterHL.w;
			}
			set
			{
				_RegisterHL.w = value;
			}
		}

		/// <summary>
		/// Access to bits 8-15 of HL
		/// </summary>
		public byte H
		{
			get
			{
				return _RegisterHL.h.Value;
			}
			set
			{
				_RegisterHL.h.Value = value;
			}
		}

		/// <summary>
		/// Access to bits 0-7 of HL
		/// </summary>
		public byte L
		{
			get
			{
				return _RegisterHL.l.Value;
			}
			set
			{
				_RegisterHL.l.Value = value;
			}
		}


		#endregion
		
		#region Register SP

		/// <summary>
		/// Stack pointer register
		/// </summary>
		public Register RegisterSP
		{
			get
			{
				return _RegisterSP;
			}
			set
			{
				_RegisterSP = value;
			}
		}

		/// <summary>
		/// Access to SP register as word
		/// </summary>
		public ushort SP
		{
			get
			{
				return _RegisterSP.w;
			}
			set
			{
				_RegisterSP.w = value;
			}
		}


		#endregion


		#region Alternate registers (AF', BC', DE', HL')
		
		/// <summary>
		/// Alternate Accumulator and Flags Register
		/// </summary>
		public Register RegisterAF_
		{
			get
			{
				return _RegisterAF_;
			}
			set
			{
				_RegisterAF_ = value;
			}
		}

		/// <summary>
		/// Alternate BC Register
		/// </summary>
		public Register RegisterBC_
		{
			get
			{
				return _RegisterBC_;
			}
			set
			{
				_RegisterBC_ = value;
			}
		}

		/// <summary>
		/// Alternate DE Register
		/// </summary>
		public Register RegisterDE_
		{
			get
			{
				return _RegisterDE_;
			}
			set
			{
				_RegisterDE_ = value;
			}
		}

		/// <summary>
		/// Alternate HL Register
		/// </summary>
		public Register RegisterHL_
		{
			get
			{
				return _RegisterHL_;
			}
			set
			{
				_RegisterHL_ = value;
			}
		}

		#endregion


		#region Index Registers


		/// <summary>
		/// Index register IX
		/// </summary>
		public Register RegisterIX
		{
			get
			{
				return _RegisterIX;
			}
			set
			{
				_RegisterIX = value;
			}
		}

		/// <summary>
		/// Access to IX register as word
		/// </summary>
		public ushort IX
		{
			get
			{
				return _RegisterIX.w;
			}
			set
			{
				_RegisterIX.w = value;
			}
		}


		/// <summary>
		/// Index register IY
		/// </summary>
		public Register RegisterIY
		{
			get
			{
				return _RegisterIY;
			}
			set
			{
				_RegisterIY = value;
			}
		}

		/// <summary>
		/// Access to IY register as word
		/// </summary>
		public ushort IY
		{
			get
			{
				return _RegisterIY.w;
			}
			set
			{
				_RegisterIY.w = value;
			}
		}

		#endregion


		#region IR Register

		/// <summary>
		/// Interrupt register
		/// </summary>
		public byte I
		{
			get
			{
				return _I;
			}
			set
			{
				_I = value;
			}
		}

		/// <summary>
		/// Refresh register
		/// </summary>
		public byte R
		{
			get
			{
				return _R;
			}
			set
			{
				_R = value;
			}
		}

		/// <summary>
		/// Refresh register Bit 7
		/// </summary>
		public byte R7
		{
			get
			{
				return _R7;
			}
			set
			{
				_R7 = value;
			}
		}

		#endregion


		#region Program Counter

		/// <summary>
		/// Program counter
		/// </summary>
		public ushort PC
		{
			get
			{
				return _PC;
			}
			set
			{
				_PC = value;
			}
		}

		#endregion


		#region Other states holders


		/// <summary>
		/// CPU Halted
		/// </summary>
		public bool Halted
		{
			get
			{
				return _Halted;
			}
			set
			{
				_Halted = value;
			}
		}

		/// <summary>
		/// Main interrupts flip flop
		/// </summary>
		public bool IFF1
		{
			get
			{
				return _IFF1;
			}
			set
			{
				_IFF1 = value;
			}
		}

		/// <summary>
		/// Temporary storage for IFF1
		/// </summary>
		public bool IFF2
		{
			get
			{
				return _IFF2;
			}
			set
			{
				_IFF2 = value;
			}
		}

		
		/// <summary>
		/// Interrupt Mode (can be 0, 1, 2)
		/// </summary>
		public byte IM
		{
			get
			{
				return _IM;
			}
			set
			{
				_IM = value;
			}
		}

		#endregion
	

		/// <summary>
		/// Resets the Z80 status
		/// </summary>
		public void Reset()
		{
			AF = 0;
			BC = 0;
			DE = 0;
			HL = 0;

			RegisterAF_.w = 0;
			RegisterBC_.w = 0;
			RegisterDE_.w = 0;
			RegisterHL_.w = 0;

			IX = 0;
			IY = 0;

			_I = 0;
			_R = 0;
			_R7 = 0;

			SP = 0xFFFF;
			_PC = 0;
			
			_IFF1 = false;
			_IFF2 = false;
			_IM = 0;
			
			_Halted = false;
		}
	
		/// <summary>
		/// Serializes the status in a text stream
		/// </summary>
		/// <param name="stream"></param>
		public void Serialize(System.IO.TextWriter stream)
		{
			stream.WriteLine("PC= {0}, SP= {1}", PC, SP);
			stream.WriteLine("A= {0}, F= {1}, I= {2}, R= {3}, BC= {4}, DE= {5}, HL= {6}", A, F, I, R, BC, DE, HL);
			stream.WriteLine("AF'= {0}, BC'= {1}, DE'= {2}, HL'= {3}", RegisterAF_.w, RegisterBC_.w, RegisterDE_.w, RegisterHL_.w);
			stream.WriteLine("IX= {0}, IY= {1}", IX, IY);
			stream.WriteLine("IM= {0}, IFF1= {1}, IFF2= {2}", IM, IFF1 ? 1 : 0, IFF2 ? 1 : 0);
			stream.WriteLine();
		}

		/// <summary>
		/// Make a copy of this class
		/// </summary>
		/// <returns>A new status class containing this status</returns>
		public Status Clone()
		{
			Status _s = new Status();

			_s.AF = AF;
			_s.BC = BC;
			_s.DE = DE;
			_s.HL = HL;

			_s.RegisterAF_.w = RegisterAF_.w;
			_s.RegisterBC_.w = RegisterBC_.w;
			_s.RegisterDE_.w = RegisterDE_.w;
			_s.RegisterHL_.w = RegisterHL_.w;

			_s.IX = IX;
			_s.IY = IY;

			_s._I = _I;
			_s._R = _R;
			_s._R7 = _R7;

			_s.SP = SP;
			_s._PC = _PC;
			
			_s._IFF1 = _IFF1;
			_s._IFF2 = _IFF2;
			_s._IM = _IM;
			
			_s._Halted = _Halted;

			return _s;
		}
	
	}

	/// <summary>
	/// Definition of F register content
	/// </summary>
	public class FlagRegisterDefinition
	{
		/// <summary>
		/// Carry flag
		/// </summary>
		public const byte C = 0x01;
		/// <summary>
		/// Add/Subtract flag
		/// </summary>
		public const byte N = 0x02;
		/// <summary>
		/// Parity flag
		/// </summary>
		public const byte P = 0x04;
		/// <summary>
		/// Overflow flag
		/// </summary>
		public const byte V = 0x04;
		/// <summary>
		/// Not used
		/// </summary>
		public const byte _3 = 0x08;
		/// <summary>
		/// Half carry flag
		/// </summary>
		public const byte H = 0x10;
		/// <summary>
		/// Not used
		/// </summary>
		public const byte _5 = 0x20;
		/// <summary>
		/// Zero flag
		/// </summary>
		public const byte Z = 0x40;
		/// <summary>
		/// Sign flag
		/// </summary>
		public const byte S = 0x80;
	}

}
