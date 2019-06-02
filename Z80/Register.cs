using System;

namespace Z80
{
	/// <summary>
	/// Class containing a single register.
	/// </summary>
	public class Register
	{

		private HalfRegister _l = new HalfRegister();
		private HalfRegister _h = new HalfRegister();

		/// <summary>
		/// High bits of the register
		/// </summary>
		public HalfRegister h
		{
			get
			{
				return _h;
			}
		}

		/// <summary>
		/// Low bits of the register
		/// </summary>
		public HalfRegister l
		{
			get
			{
				return _l;
			}
		}


		/// <summary>
		/// Register
		/// </summary>
		public ushort w
		{
			get
			{
				return (ushort) ((_h.Value << 8) | (_l.Value));
			}
			set
			{
				_h.Value = (byte) ((value >> 8) & 0xFF);
				_l.Value = (byte) (value & 0xFF);
			}
		}

		/// <summary>
		/// Used to swap this register with another
		/// </summary>
		/// <param name="Register">Register to swap with this</param>
		public void Swap(Register Register)
		{
			byte _hValue = Register.h.Value;
			byte _lValue = Register.l.Value;

			Register.h.Value = _h.Value;
			Register.l.Value = _l.Value;

			_h.Value = _hValue;
			_l.Value = _lValue;
		}

	}

	/// <summary>
	/// Class containing half register
	/// </summary>
	public class HalfRegister
	{
		
		/// <summary>
		/// Main constructor
		/// </summary>
		public HalfRegister(){}

		/// <summary>
		/// Constructor with initial value
		/// </summary>
		/// <param name="Value">Initial value</param>
		public HalfRegister(byte Value)
		{
			_Value = Value;
		}
		
		private byte _Value;
		
		/// <summary>
		/// Half register value
		/// </summary>
		public byte Value
		{
			get
			{
				return _Value;
			}
			set
			{
				_Value = value;
			}
		}
	}

}
