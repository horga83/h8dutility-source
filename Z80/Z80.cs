using System;

namespace Z80
{
	/// <summary>
	/// Class rappresenting a system based on Z80
	/// </summary>
	public class uP
	{

		// Memory and IO access
		private IMemory _Memory;
		private IIO _IO;

		private Status _Status = new Status();
		public int tstates;

		// By default fetching won't be stopped
		private int _StatementsToFetch = -1;


		/// <summary>
		/// Handler of a fetch event (not a standard M$ event declaration but...)
		/// </summary>
		public delegate void OnFetchHandler();
		
		/// <summary>
		/// Fetch event (used during debug)
		/// </summary>
		public event OnFetchHandler OnFetch;


		//Whether a half carry occured or not can be determined by looking at
		//the 3rd bit of the two arguments and the result; these are hashed
		//into this table in the form r12, where r is the 3rd bit of the
		//result, 1 is the 3rd bit of the 1st argument and 2 is the
		//third bit of the 2nd argument; the tables differ for add and subtract
		//operations 
		private byte[] LookupTable_halfcarry_add = { 0, FlagRegisterDefinition.H, FlagRegisterDefinition.H, FlagRegisterDefinition.H, 0, 0, 0, FlagRegisterDefinition.H };
		private byte[] LookupTable_halfcarry_sub = { 0, 0, FlagRegisterDefinition.H, 0, FlagRegisterDefinition.H, 0, FlagRegisterDefinition.H, FlagRegisterDefinition.H };

		//Similarly, overflow can be determined by looking at the 7th bits; again
		//the hash into this table is r12
		private byte[] LookupTable_overflow_add = { 0, 0, 0, FlagRegisterDefinition.V, FlagRegisterDefinition.V, 0, 0, 0 };
		private byte[] LookupTable_overflow_sub = { 0, FlagRegisterDefinition.V, 0, 0, 0, 0, FlagRegisterDefinition.V, 0 };

		
#warning Come vengono usate queste tabelle???
		//Some more tables; initialised in LookupTables_init()
		private byte[] LookupTable_sz53 = new byte[0x100];   // The S, Z, 5 and 3 bits of the lookup value
		private byte[] LookupTable_parity = new byte[0x100]; // The parity of the lookup value
		private byte[] LookupTable_sz53p = new byte[0x100];  // OR the above two tables together



		/// <summary>
		/// Initialise tables used to determine flags
		/// </summary>
		private void LookupTables_Init()
		{
			int i,j,k;
			byte parity;

			for(i = 0; i < 0x100; i++) 
			{
				LookupTable_sz53[i] = (byte) (i & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 | FlagRegisterDefinition.S ));
				
				j = i;
				parity = 0;
				for(k = 0; k < 8; k++)
				{
					parity ^= (byte) (j & 1);
					j >>=1;
				}
				LookupTable_parity[i]= ( parity != 0 ? (byte) 0 : FlagRegisterDefinition.P);
				LookupTable_sz53p[i] = (byte) (LookupTable_sz53[i] | LookupTable_parity[i]);
			}

			LookupTable_sz53[0]  |= FlagRegisterDefinition.Z;
			LookupTable_sz53p[0] |= FlagRegisterDefinition.Z;

		}




		/// <summary>
		/// Create new system based on Z80
		/// </summary>
		/// <param name="Memory">System memory</param>
		/// <param name="IO">IO</param>
		public uP(IMemory Memory, IIO IO)
		{
			_Memory = Memory;
			_IO = IO;
			LookupTables_Init();
			Reset();
		}

		/// <summary>
		/// System memory
		/// </summary>
		public IMemory Memory
		{
			get
			{
				return _Memory;
			}
		}

		/// <summary>
		/// System IO
		/// </summary>
		public IIO IO
		{
			get
			{
				return _IO;
			}
		}

		/// <summary>
		/// Z80 internal status
		/// </summary>
		public Status Status
		{
			get
			{
				return _Status;
			}
		}


		/// <summary>
		/// Resets the system
		/// </summary>
		public void Reset()
		{
			_Status.Reset();
		}


		/// <summary>
		/// Raise (process) a Z80 maskable interrupt
		/// </summary>
		public void Interrupt()
		{
			// Check if the interrupts are enabled
			if(_Status.IFF1)
			{
    
				// The Z80 is no more halted
				_Status.Halted = false;
    
				// Reset Interrupt Flip Flops
				// When the CPU accepts a maskable interrupt, both IFF1 and IFF2 are automatically reset,
				// inhibiting further interrupts until the programmer issues a new El instruction.
				_Status.IFF1 = false;
				_Status.IFF2 = false;


				// Push program counter
				Push(_Status.PC);

				switch(_Status.IM) 
				{
					case 0: 
						//_Status.PC = 0x0038;
                        _Status.PC = (ushort)(_Status.I << 3);
						tstates+=12; 
						break;
					case 1: 
						_Status.PC = 0x0038; 
						tstates+=13; 
						break;
					case 2: 
						ushort InterruptTableAddress = (ushort) ((_Status.I << 8) | 0xFF);
						_Status.PC = _Memory.ReadWord(InterruptTableAddress);
						tstates+=19;
						break;
					default:
						Console.Error.WriteLine("Unknown interrupt mode {0}\n", _Status.IM);
						break;
#warning Here the program should exit
				}
			}
		}

		#region Misc functions

		/// <summary>
		/// From an opcode returns a half register following the rules
		/// Reg opcode
		///   A xxxxx111
		///   B xxxxx000
		///   C xxxxx001
		///   D xxxxx010
		///   E xxxxx011
		///   H xxxxx100
		///   L xxxxx101
		/// </summary>
		/// <param name="opcode">opcode</param>
		/// <returns>The half register or null if opcode is 110 (it is the ID for (HL))</returns>
		private HalfRegister GetHalfRegister(byte opcode)
		{
			switch (opcode & 0x07)
			{
				case 0x00:
					return _Status.RegisterBC.h;
				case 0x01:
					return _Status.RegisterBC.l;
				case 0x02:
					return _Status.RegisterDE.h;
				case 0x03:
					return _Status.RegisterDE.l;
				case 0x04:
					return _Status.RegisterHL.h;
				case 0x05:
					return _Status.RegisterHL.l;
				case 0x06:
					return null;
				case 0x07:
					return _Status.RegisterAF.h;
			}
			throw new Exception("Why am I here?");
		}


		/// <summary>
		/// From an opcode return a register following the rules
		/// Reg         opcode
		///  BC         xx00xxxx
		///  DE         xx01xxxx
		///  HL         xx10xxxx
		///  SP or AF   xx11xxxx
		/// </summary>
		/// <param name="opcode">opcode</param>
		/// <param name="ReturnSP">Checked if opcode is 11. If this parameter is true then SP is returned else AF is returned</param>
		/// <returns>The register</returns>
		private Register GetRegister(byte opcode, bool ReturnSP)
		{
			switch (opcode & 0x30)
			{
				case 0x00:
					return _Status.RegisterBC;
				case 0x10:
					return _Status.RegisterDE;
				case 0x20:
					return _Status.RegisterHL;
				case 0x30:
					if (ReturnSP)
						return _Status.RegisterSP;
					else
						return _Status.RegisterAF;
			}
			throw new Exception("What's happening to me?");
		}


		/// <summary>
		/// Check the flag related to opcode according with the following table:
		/// Cond opcode   Flag Description
		/// NZ   xx000xxx  Z   Not Zero
		///  Z   xx001xxx  Z   Zero
		/// NC   xx010xxx  C   Not Carry
		///  C   xx011xxx  C   Carry
		/// PO   xx100xxx  P/V Parity odd  (Not parity)
		/// PE   xx101xxx  P/V Parity even (Parity)
		///  P   xx110xxx  S   Sign positive
		///  M   xx111xxx  S   Sign negative
		/// </summary>
		/// <param name="opcode">The opcode</param>
		/// <returns>True if the condition is satisfied otherwise false</returns>
		private bool CheckFlag(byte opcode)
		{
			bool Not = false;
			byte Flag;
			
			// Find the right flag and the condition
			switch ((opcode >> 3) & 0x07)
			{
				case 0:
					Not = true;
					Flag = FlagRegisterDefinition.Z;
					break;
				case 1:
					Flag = FlagRegisterDefinition.Z;
					break;
				case 2:
					Not = true;
					Flag = FlagRegisterDefinition.C;
					break;
				case 3:
					Flag = FlagRegisterDefinition.C;
					break;
				case 4:
					Not = true;
					Flag = FlagRegisterDefinition.P;
					break;
				case 5:
					Flag = FlagRegisterDefinition.P;
					break;
				case 6:
					Not = true;
					Flag = FlagRegisterDefinition.S;
					break;
				case 7:
					Flag = FlagRegisterDefinition.S;
					break;
				default:
					throw new Exception("I'm feeling bad");
			}

			// Check flag and condition
			if (Not)
				return ((_Status.F & Flag) == 0);
			else
				return ((_Status.F & Flag) != 0);


		}

		#endregion

		#region Stack access
		
		/// <summary>
		/// Push a register in stack and decrement SP
		/// </summary>
		/// <param name="Register">Register to push</param>
		private void Push(Register Register)
		{
			Push(Register.w);
		}

		/// <summary>
		/// Push a byte in stack and decrement SP
		/// </summary>
		/// <param name="Byte">Byte to push</param>
		private void Push(byte Byte)
		{
			_Status.SP--;
			_Memory.WriteByte(_Status.SP, Byte);
		}

		/// <summary>
		/// Push a word in stack and decrement SP
		/// </summary>
		/// <param name="Word">Word to push</param>
		private void Push(ushort Word)
		{
			_Status.SP -= 2;
			_Memory.WriteWord(_Status.SP, Word);
		}

		/// <summary>
		/// Pop a register from stack and increment SP
		/// </summary>
		/// <param name="Register">Register to pop</param>
		private void Pop(Register Register)
		{
			ushort MemoryContent;
			Pop(out MemoryContent);
			Register.w = MemoryContent;
		}

		/// <summary>
		/// Pop a byte from stack and increment SP
		/// </summary>
		/// <param name="Byte">Byte to pop</param>
		private void Pop(out byte Byte)
		{
			Byte = _Memory.ReadByte(_Status.SP);
			_Status.SP++;
		}

		/// <summary>
		/// Pop a word from stack and increment SP
		/// </summary>
		/// <param name="Word">Word to pop</param>
		private void Pop(out ushort Word)
		{
			Word = _Memory.ReadWord(_Status.SP);
			_Status.SP += 2;
		}

		#endregion

		#region ALU

		/// <summary>
		/// A logical AND operation is performed between the byte specified by the op
		/// operand and the byte contained in the Accumulator; the result is stored in
		/// the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set
		/// P/V is reset if overflow; reset otherwise
		/// N is reset
		/// C is reset
		/// </summary>
		/// <param name="op">The operand</param>
		private void AND_A(byte op)
		{
			_Status.A &= op;
			_Status.F = (byte) 
				(FlagRegisterDefinition.H |
				LookupTable_sz53p[_Status.A]);
		}

		/// <summary>
		/// The op operand, along with the Carry Flag (C in the F register) is added to the
		/// contents of the Accumulator, and the result is stored in the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if carry from bit 3; reset otherwise
		/// P/V is set if overflow; reset otherwise
		/// N is reset
		/// C is set if carry from bit 7: reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void ADC_A(byte op)
		{
			ushort result = (ushort) (_Status.A + op + ( (_Status.F & FlagRegisterDefinition.C) != 0 ? 1 : 0));
			// Prepare the bits to perform the lookup
			byte lookup = (byte) (( (_Status.A & 0x88) >> 3 ) | ( ( op & 0x88 ) >> 2 ) | ( (result & 0x88) >> 1 ));
			_Status.A = (byte) result;

			_Status.F = (byte) 
				(((result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte) 0 ) |
				LookupTable_halfcarry_add[lookup & 0x07] |
				LookupTable_overflow_add[lookup >> 4] |
				LookupTable_sz53[_Status.A]);
		}


		/// <summary>
		/// The op operand, along with the Carry Flag (C in the F register) is added to the
		/// contents of HL register, and the result is stored in HL register.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if carry from bit 11; reset otherwise
		/// P/V is set if overflow; reset otherwise
		/// N is reset
		/// C is set if carry from bit 15: reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void ADC_HL(ushort op)
		{
			uint result = (uint) (_Status.HL + op + ( (_Status.F & FlagRegisterDefinition.C) != 0 ? 1 : 0));
			byte lookup = (byte) (
				(byte) ( ( _Status.HL & 0x8800 ) >> 11 ) | 
				(byte) ( ( op & 0x8800 ) >> 10 ) | 
				(byte) ( ( result & 0x8800 ) >> 9 ));
			_Status.HL = (ushort) result;
			_Status.F = (byte) 
				(((result & 0x10000) != 0 ? FlagRegisterDefinition.C : (byte) 0 ) | 
				LookupTable_overflow_add[lookup >> 4] |
				( _Status.H & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 | FlagRegisterDefinition.S ) ) |
				LookupTable_halfcarry_add[lookup & 0x07] |
				( _Status.HL == 0 ? (byte) 0 : FlagRegisterDefinition.Z ));
		}

		/// <summary>
		/// The op operand is added to the
		/// contents of the Accumulator, and the result is stored in the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if carry from bit 3; reset otherwise
		/// P/V is set if overflow; reset otherwise
		/// N is reset
		/// C is set if carry from bit 7: reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void ADD_A(byte op)
		{
			ushort result = (ushort) (_Status.A + op);
			byte lookup = (byte) (( (_Status.A & 0x88) >> 3 ) | ( ( (op) & 0x88 ) >> 2 ) | ( (result & 0x88) >> 1 ));
			_Status.A = (byte) result;
			_Status.F = (byte) 
				(((result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte) 0 ) |
				LookupTable_halfcarry_add[lookup & 0x07] |
				LookupTable_overflow_add[lookup >> 4] |
				LookupTable_sz53[_Status.A]);
		}


		/// <summary>
		/// The op1 operand,  is added to the
		/// contents of op2 register, and the result is stored in op1 register.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is set if carry from bit 11; reset otherwise
		/// P/V is not affected
		/// N is reset
		/// C is set if carry from bit 15: reset otherwise
		/// </summary>
		/// <param name="op1">First operand</param>
		/// <param name="op2">Second operand</param>
		private void ADD_16(Register op1, ushort op2)
		{
			uint result = (uint) (op1.w + op2);
			byte lookup = (byte) ( 
				(byte) ( ( op1.w & 0x0800 ) >> 11 ) |
				(byte) ( ( op2 & 0x0800 ) >> 10 ) | 
				(byte) ( ( result & 0x0800 ) >> 9 ));
			op1.w = (ushort) result;
			_Status.F = (byte) (
				( _Status.F & ( FlagRegisterDefinition.V | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) |
				( (result & 0x10000) != 0 ? FlagRegisterDefinition.C : (byte) 0 )|
				(byte) ( ( result >> 8 ) & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) |
				LookupTable_halfcarry_add[lookup]);
		}

		/// <summary>
		/// This instruction tests bit bit in operand op and sets the Z flag accordingly.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if specified bit is 0; reset otherwise
		/// H is set
		/// P/V is unknown
		/// N is reset
		/// C is not affected
		/// </summary>
		/// <param name="bit">The bit to test</param>
		/// <param name="op">The operand</param>
		private void BIT(byte bit, byte op)
		{
			_Status.F = (byte) (
				( _Status.F & FlagRegisterDefinition.C ) | 
				FlagRegisterDefinition.H |
				( op & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) | 
				( ( op & ( 0x01 << bit ) ) != 0 ? 0 : ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z ) ));
		}


		/// <summary>
		/// This instruction tests bit 7 in operand op and sets the Z flag accordingly.
		/// Condition Bits Affected:
		/// S is affected if bit 7 is set
		/// Z is set if specified bit is 0; reset otherwise
		/// H is set
		/// P/V is unknown
		/// N is reset
		/// C is not affected
		/// </summary>
		/// <param name="op">The operand</param>
		private void BIT7(byte op)
		{
			_Status.F = (byte) (
				( _Status.F & FlagRegisterDefinition.C ) | 
				FlagRegisterDefinition.H |
				( op & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) |	
				( ( op & 0x80 ) != 0 ? FlagRegisterDefinition.S :	(byte) ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z ) ));
		}


		/// <summary>
		/// Operation: (SP-1) = PCH, (SP-2) = PCL, PC = nn
		/// Description: The current contents of the Program Counter (PC) are pushed onto the top
		/// of the external memory stack. The operands nn are then loaded to the PC to point to the 
		/// address in memory where the first Op Code of a subroutine is to be fetched. At the 
		/// end of the subroutine, a RETurn instruction can be used to return to the original program 
		/// flow by popping the top of the stack back to the PC. The push is accomplished by first 
		/// decrementing the current contents of the Stack Pointer (register pair SP), loading the 
		/// high-order byte of the PC contents to the memory address now pointed to by the SP; then
		/// decrementing SP again, and loading the low order byte of the PC contents to the top of 
		/// stack.
		/// Because this is a 3-byte instruction, the Program Counter was incremented by three before 
		/// the push is executed.
		/// </summary>
		private void CALL()
		{
			ushort address = Memory.ReadWord(_Status.PC);
			Push((ushort) (_Status.PC + 2));
			_Status.PC = address;
		}


		/// <summary>
		/// The Carry flag in the F register is inverted.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H, previous carry is copied
		/// P/V is not affected
		/// N is reset
		/// C is set if CY was 0 before operation; reset otherwise
		/// </summary>
		private void CCF()
		{
			_Status.F = (byte) (
				( _Status.F & ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) |
				( ( _Status.F & FlagRegisterDefinition.C ) != 0 ? FlagRegisterDefinition.H : FlagRegisterDefinition.C ) | 
				( _Status.A & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ));
		}


		/// <summary>
		/// The contents of the op operand are compared (same of sub) with the contents of the
		/// Accumulator. If there is a true compare, the Z flag is set. The execution of
		/// this instruction does not affect the contents of the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is set if overflow; reset otherwise
		/// N is set
		/// C is set if borrow; reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void CP(byte op)
		{
			ushort result = (ushort) (_Status.A - op);
			byte lookup = (byte) ((( (_Status.A & 0x88) >> 3 ) | ( ( op & 0x88 ) >> 2 ) | ( (result & 0x88) >> 1 )));
			_Status.F = (byte) (
				( (result & 0x100) != 0 ? FlagRegisterDefinition.C : ( result != 0 ? (byte) 0 : FlagRegisterDefinition.Z ) ) |
				FlagRegisterDefinition.N | 
				LookupTable_halfcarry_sub[lookup & 0x07] | 
				LookupTable_overflow_sub[lookup >> 4] |  
				( op & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) |  
				( result & FlagRegisterDefinition.S ));
		}



		/// <summary>
		/// ???????????????????????????????????????????????????????????
		/// </summary>
		private delegate void instruction(byte target);
		/* Macro for the {DD,FD} CB dd xx rotate/shift instructions */
#warning questa signature non aveva tempaddr
		/// <summary>
		/// 
		/// </summary>
		/// <param name="time"></param>
		/// <param name="target"></param>
		/// <param name="instruction"></param>
		/// <param name="tempaddr"></param>
		private void DDFDCB_ROTATESHIFT(byte time, byte target,  instruction instruction, ushort tempaddr)
		{
			tstates += time;
			target = _Memory.ReadByte( tempaddr );
			instruction( target );
			_Memory.WriteByte( tempaddr, target );
#warning Alla fine di questa sub c'era un break per uscire da un qualche ciclo
		}

		/// <summary>
		/// The contents of the memory location addressed by the HL register pair is
		/// compared with the contents of the Accumulator. In case of a true
		/// compare, a condition bit is set. The HL and Byte Counter (register pair
		/// BC) are decremented.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if A is (HL); reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is set if BC -1 is not 0; reset otherwise
		/// N is set
		/// C is not affected
		/// </summary>
		private void CPD()
		{
			CPx();
			_Status.HL--; 
		}

		/// <summary>
		/// The contents of the memory location addressed by the HL register pair is
		/// compared with the contents of the Accumulator. In case of a true compare,
		/// a condition bit is set. The HL and BC (Byte Counter) register pairs are
		/// decremented. If decrementing causes the BC to go to zero or if A = (HL),
		/// the instruction is terminated. If BC is not zero and A = (HL), the program
		/// counter is decremented by two and the instruction is repeated. Interrupts are
		/// recognized and two refresh cycles execute after each data transfer. When
		/// BC is set to zero, prior to instruction execution, the instruction loops
		/// through 64 Kbytes if no match is found.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if A = (HL); reset otherwise
		/// H is set if borrow form bit 4; reset otherwise
		/// P/V is set if BC -1 != 0; reset otherwise
		/// N is set
		/// C is not affected
		/// </summary>
		private void CPDR()
		{
			CPx();
			_Status.HL--;
			if( ( _Status.F & ( FlagRegisterDefinition.V | FlagRegisterDefinition.Z ) ) == FlagRegisterDefinition.V ) 
			{ 
				tstates += 5; 
				_Status.PC -= 2; 
			}
		}



		/// <summary>
		/// The contents of the memory location addressed by the HL register is
		/// compared with the contents of the Accumulator. In case of a true compare,
		/// a condition bit is set. Then HL is incremented and the Byte Counter
		/// (register pair BC) is decremented.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if A is (HL); reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is set if BC -1 is not 0; reset otherwise
		/// N is set
		/// C is not affected
		/// </summary>
		private void CPI()
		{
			CPx();
			_Status.HL++; 
		}

		/// <summary>
		/// The contents of the memory location addressed by the HL register pair is
		/// compared with the contents of the Accumulator. In case of a true compare, a
		/// condition bit is set. HL is incremented and the Byte Counter (register pair
		/// BC) is decremented. If decrementing causes BC to go to zero or if A = (HL),
		/// the instruction is terminated. If BC is not zero and A != (HL), the program
		/// counter is decremented by two and the instruction is repeated. Interrupts are
		/// recognized and two refresh cycles are executed after each data transfer.
		/// If BC is set to zero before instruction execution, the instruction loops
		/// through 64 Kbytes if no match is found.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if A equals (HL); reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is set if BC -1 does not equal 0; reset otherwise
		/// N is set
		/// C is not affected
		/// </summary>
		private void CPIR()
		{
			CPx();
			_Status.HL++;
			if( ( _Status.F & ( FlagRegisterDefinition.V | FlagRegisterDefinition.Z ) ) == FlagRegisterDefinition.V ) 
			{ 
				tstates += 5; 
				_Status.PC -= 2; 
			}
		}


		/// <summary>
		/// Used by CPD, CPI, CPDR, CPIR
		/// The contents of the memory location addressed by the HL register is
		/// compared with the contents of the Accumulator. In case of a true compare,
		/// a condition bit is set. Then the Byte Counter(register pair BC) is 
		/// decremented.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if A is (HL); reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is set if BC -1 is not 0; reset otherwise
		/// N is set
		/// C is not affected
		/// </summary>
		private void CPx()
		{
			byte _b = _Memory.ReadByte(_Status.HL);
			byte result = (byte) (_Status.A - _b);
			byte lookup = (byte) (((Status.A & 0x08) >> 3 ) | ( ( (_b) & 0x08 ) >> 2) | ((result & 0x08) >> 1));
			_Status.BC--;
			_Status.F = (byte) ((_Status.F & FlagRegisterDefinition.C ) | ( _Status.BC != 0 ? (byte) ( FlagRegisterDefinition.V | FlagRegisterDefinition.N ) : FlagRegisterDefinition.N ) | LookupTable_halfcarry_sub[lookup] | ( result != 0 ? (byte) 0 : FlagRegisterDefinition.Z ) |	( result & FlagRegisterDefinition.S ));
			if((_Status.F & FlagRegisterDefinition.H) != 0)
				result--;
			_Status.F |= (byte) (( result & FlagRegisterDefinition._3 ) | ( (result & 0x02) != 0 ? FlagRegisterDefinition._5 : (byte) 0 ));
		}


		/// <summary>
		/// The contents of the Accumulator (register A) are inverted (one’s
		/// complement).
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is set
		/// P/V is not affected
		/// N is set
		/// C is not affected
		/// </summary>
		private void CPL()
		{
			_Status.A ^= 0xFF;
			_Status.F =(byte) (( _Status.F & ( FlagRegisterDefinition.C | FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) | ( _Status.A & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) | ( FlagRegisterDefinition.N | FlagRegisterDefinition.H ));
		}

		
		/// <summary>
		/// This instruction conditionally adjusts the Accumulator for BCD addition and
		/// subtraction operations. For addition (ADD, ADC, INC) or subtraction (SUB,
		/// SBC, DEC, NEG)
		/// </summary>
		private void DAA()
		{
			byte add = 0;
			byte carry = (byte) ( _Status.F & FlagRegisterDefinition.C );
			if( ( _Status.F & FlagRegisterDefinition.H ) != 0 || ( (_Status.A & 0x0F) > 9 ) )
				add = 6;
			if( carry != 0 || (_Status.A > 0x9F ) ) 
				add |= 0x60;
			if( _Status.A > 0x99 ) 
				carry = 1;
			if ( (_Status.F & FlagRegisterDefinition.N) != 0 ) 
			{
				SUB(add);
			} 
			else 
			{
				if( (_Status.A>0x90) && ( (_Status.A & 0x0F)>9) ) 
					add|=0x60;
				ADD_A(add);
			}
			_Status.F = (byte) (( _Status.F & ~( FlagRegisterDefinition.C | FlagRegisterDefinition.P) ) | carry | LookupTable_parity[_Status.A]);
		}

		/// <summary>
		/// The byte specified by the op operand is decremented
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if borrow from bit 4, reset otherwise
		/// P/V is set if m was 80H before operation; reset otherwise
		/// N is set
		/// C is not affected
		/// </summary>
		/// <param name="op">The operand</param>
		private void DEC(HalfRegister op)
		{
			_Status.F = (byte) (( _Status.F & FlagRegisterDefinition.C ) | ( (op.Value & 0x0F) != 0 ? (byte) 0 : FlagRegisterDefinition.H ) | FlagRegisterDefinition.N);
			op.Value--;
			_Status.F |= (byte) (( op.Value == 0x79 ? FlagRegisterDefinition.V : (byte) 0 ) | LookupTable_sz53[op.Value]);
		}

		
		/// <summary>
		/// The contents of port are placed on the address bus to select the I/O device at 
		/// one of 256 possible ports.
		/// Usually port is BC (register BC) or An (Accumulator + number)
		/// Then one byte from the selected port is placed on
		/// the data bus and written to register reg in the CPU.
		/// The flags are affected, checking the input data.
		/// Condition Bits Affected:
		/// S is set if input data is negative; reset otherwise
		/// Z is set if input data is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity is even; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		/// <param name="reg">The register</param>
		/// <param name="port">The port</param>
		private void IN(HalfRegister reg, ushort port)
		{
			reg.Value = _IO.ReadPort(port);
			_Status.F = (byte) (( _Status.F & FlagRegisterDefinition.C) | LookupTable_sz53p[reg.Value]);
		}


		/// <summary>
		/// The byte contained in op is incremented.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if carry from bit 3; reset otherwise
		/// P/V is set if op was 7FH before operation; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		/// <param name="op">The operand</param>
		private void INC(HalfRegister op)
		{
			op.Value++;
			_Status.F = (byte) (
				( _Status.F & FlagRegisterDefinition.C ) | 
				( op.Value == 0x80 ? FlagRegisterDefinition.V : (byte) 0 ) |
				( (op.Value & 0x0F) != 0 ? (byte) 0 : FlagRegisterDefinition.H ) | 
				( op.Value != 0 ? (byte) 0 : FlagRegisterDefinition.Z ) | 
				LookupTable_sz53[op.Value]);
		}


		/// <summary>
		/// The contents of register C are placed on the bottom half (A0 through A7) of
		/// the address bus to select the I/O device at one of 256 possible ports.
		/// Register B may be used as a byte counter, and its contents are placed on the
		/// top half (A8 through A15) of the address bus at this time. Then one byte
		/// from the selected port is placed on the data bus and written to the CPU. The
		/// contents of the HL register pair are placed on the address bus and the input
		/// byte is written to the corresponding location of memory. Finally, the byte
		/// counter and register pair HL are decremented.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if B–1 = 0; reset otherwise
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void IND()
		{
			INx();
			_Status.HL--;
		}


		/// <summary>
		/// The contents of register C are placed on the bottom half (A0 through A7)
		/// of the address bus to select the I/O device at one of 256 possible ports.
		/// Register B is used as a byte counter, and its contents are placed on the top
		/// half (A8 through A15) of the address bus at this time. Then one byte from
		/// the selected port is placed on the data bus and written to the CPU. The
		/// contents of the HL register pair are placed on the address bus and the
		/// input byte is written to the corresponding location of memory. Then HL
		/// and the byte counter are decremented. If decrementing causes B to go to
		/// zero, the instruction is terminated. If B is not zero, the PC is decremented
		/// by two and the instruction repeated. Interrupts are recognized and two
		/// refresh cycles are executed after each data transfer.
		/// When B is set to zero prior to instruction execution, 256 bytes of data are
		/// input.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// </summary>
		private void INDR()
		{
			INx();
			_Status.HL--;
			if(_Status.B != 0) 
			{ 
				tstates += 5; 
				_Status.PC-=2; 
			}
		}


		/// <summary>
		/// The contents of register C are placed on the bottom half (A0 through A7) of
		/// the address bus to select the I/O device at one of 256 possible ports.
		/// Register B may be used as a byte counter, and its contents are placed on the
		/// top half (A8 through A15) of the address bus at this time. Then one byte
		/// from the selected port is placed on the data bus and written to the CPU. The
		/// contents of the HL register pair are then placed on the address bus and the
		/// input byte is written to the corresponding location of memory. Finally, the
		/// byte counter is decremented and register pair HL is incremented.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if B–1 = 0, reset otherwise
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void INI()
		{
			INx();
			_Status.HL++;
		}

		/// <summary>
		/// The contents of register C are placed on the bottom half (A0 through A7) of
		/// the address bus to select the I/O device at one of 256 possible ports.
		/// Register B is used as a byte counter, and its contents are placed on the top
		/// half (A8 through A15) of the address bus at this time. Then one byte from
		/// the selected port is placed on the data bus and written to the CPU. The
		/// contents of the HL register pair are placed on the address bus and the input
		/// byte is written to the corresponding location of memory. Then register pair
		/// HL is incremented, the byte counter is decremented. If decrementing causes
		/// B to go to zero, the instruction is terminated. If B is not zero, the PC is
		/// decremented by two and the instruction repeated. Interrupts are recognized
		/// and two refresh cycles execute after each data transfer.
		/// Note: if B is set to zero prior to instruction execution, 256 bytes of data
		/// are input.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void INIR()
		{
			INx();
			_Status.HL++;
			if(_Status.B != 0) 
			{ 
				tstates += 5; 
				_Status.PC -= 2; 
			}
		}


		/// <summary>
		/// Used by INI, IND, INIR, INDR
		/// The contents of register C are placed on the bottom half (A0 through A7) of
		/// the address bus to select the I/O device at one of 256 possible ports.
		/// Register B may be used as a byte counter, and its contents are placed on the
		/// top half (A8 through A15) of the address bus at this time. Then one byte
		/// from the selected port is placed on the data bus and written to the CPU. The
		/// contents of the HL register pair are then placed on the address bus and the
		/// input byte is written to the corresponding location of memory. Finally, the
		/// byte counter is decremented. Register pair HL must be incremented/decremented 
		/// by callers.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if B–1 = 0, reset otherwise
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void INx()
		{
			byte _b = _IO.ReadPort(_Status.BC);
			_Memory.WriteByte(_Status.HL, _b);
			_Status.B--; 
			_Status.F = (byte) (((_b & 0x80) != 0 ? FlagRegisterDefinition.N : (byte) 0 ) | LookupTable_sz53[_Status.B]);
#warning S,H and P/V flags not implemented
		}


		/// <summary>
		/// This 2-byte instruction transfers a byte of data from the memory location
		/// addressed by the contents of the HL register pair to the memory location
		/// addressed by the contents of the DE register pair. Then both of these register
		/// pairs including the BC (Byte Counter) register pair are decremented.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is set if BC -1 != 0; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		private void LDD()
		{
			LDx();
			_Status.DE--; 
			_Status.HL--;
		}


		/// <summary>
		/// This 2-byte instruction transfers a byte of data from the memory
		/// location addressed by the contents of the HL register pair to the memory
		/// location addressed by the contents of the DE register pair. Then both of
		/// these registers, as well as the BC (Byte Counter), are decremented. If
		/// decrementing causes BC to go to zero, the instruction is terminated. If
		/// BC is not zero, the program counter is decremented by two and the
		/// instruction is repeated. Interrupts are recognized and two refresh cycles
		/// execute after each data transfer.
		/// When BC is set to zero, prior to instruction execution, the instruction loops
		/// through 64 Kbytes.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is reset
		/// N is reset
		/// </summary>
		private void LDDR()
		{
			LDx();
			_Status.HL--;
			_Status.DE--;
			if(_Status.BC != 0) 
			{ 
				tstates += 4; 
				_Status.PC -= 2; 
			}

		}


		/// <summary>
		/// A byte of data is transferred from the memory location addressed, by the
		/// contents of the HL register pair to the memory location addressed by the
		/// contents of the DE register pair. Then both these register pairs are
		/// incremented and the BC (Byte Counter) register pair is decremented.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is set if BC -1 != 0; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		private void LDI()
		{
			LDx();
			_Status.DE++; 
			_Status.HL++;
		}



		/// <summary>
		/// This 2-byte instruction transfers a byte of data from the memory location
		/// addressed by the contents of the HL register pair to the memory location
		/// addressed by the DE register pair. Both these register pairs are incremented
		/// and the BC (Byte Counter) register pair is decremented. If decrementing
		/// causes the BC to go to zero, the instruction is terminated. If BC is not zero,
		/// the program counter is decremented by two and the instruction is repeated.
		/// Interrupts are recognized and two refresh cycles are executed after each
		/// data transfer. When BC is set to zero prior to instruction execution, the
		/// instruction loops through 64 Kbytes.
		/// Condition Bits Affected:
		/// S is not affected 
		/// Z is not affected 
		/// H is reset 
		/// P/V is reset 
		/// N is reset 
		/// C is not affected
		/// </summary>
		private void LDIR()
		{
			LDx();
			_Status.DE++; 
			_Status.HL++;
			if(_Status.BC != 0) 
			{ 
				tstates += 5; 
				_Status.PC-=2; 
			}
		}

		
		/// <summary>
		/// Used by LDI, LDD, LDIR, LDDR
		/// A byte of data is transferred from the memory location addressed, by the
		/// contents of the HL register pair to the memory location addressed by the
		/// contents of the DE register pair. Then the BC (Byte Counter) register 
		/// pair is decremented. Increase/Decrease of DE/HL registers must be done
		/// by the caller.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is set if BC -1 != 0; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		private void LDx()
		{
			byte _b = _Memory.ReadByte(_Status.HL);
			_Memory.WriteByte(_Status.DE,_b);
			_Status.BC--;
			_b +=  _Status.A;
			_Status.F = (byte) (( _Status.F & ( FlagRegisterDefinition.C | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) | ( _Status.BC != 0 ? FlagRegisterDefinition.V : (byte) 0 ) | ( _b & FlagRegisterDefinition._3 ) | ( (_b & 0x02) != 0 ? FlagRegisterDefinition._5 : (byte) 0 ));
		}


		/// <summary>
		/// LD (nn), dd
		/// </summary>
		/// <param name="register">dd register</param>
		private void LD_nndd(Register register)
		{
			// Read write address from PC address
			ushort address = _Memory.ReadWord(_Status.PC);
			_Status.PC += 2;

			_Memory.WriteWord(address, register.w);			
		}


		/// <summary>
		/// LD dd, (nn)
		/// </summary>
		/// <param name="register"></param>
		private void LD_ddnn(Register register)
		{
			// Read write address from PC address
			ushort address = _Memory.ReadWord(_Status.PC);
			_Status.PC += 2;

			register.w = _Memory.ReadWord(address);
		}


		/// <summary>
		/// Jump to absolute address
		/// </summary>
		private void JP()
		{
			_Status.PC = _Memory.ReadWord(_Status.PC);
		}


		/// <summary>
		/// Jump to relative address
		/// </summary>
		private void JR()
		{
			_Status.PC = (ushort) ((int) _Status.PC + (sbyte) _Memory.ReadByte(_Status.PC));
		}

		/// <summary>
		/// A logical OR operation is performed between the byte specified by the op
		/// operand and the byte contained in the Accumulator; the result is stored in
		/// the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if overflow; reset otherwise
		/// N is reset
		/// C is reset
		/// </summary>
		/// <param name="op">The operand</param>
		private void OR(byte op)
		{
			_Status.A |= op;
			_Status.F = LookupTable_sz53p[_Status.A];
		}


		/// <summary>
		/// The contents of the HL register pair are placed on the address bus to select a
		/// location in memory. The byte contained in this memory location is
		/// temporarily stored in the CPU. Then, after the byte counter (B) is
		/// decremented, the contents of register C are placed on the bottom half (A0
		/// through A7) of the address bus to select the I/O device at one of 256
		/// possible ports. Register B may be used as a byte counter, and its
		/// decremented value is placed on the top half (A8 through A15) of the
		/// address bus at this time. Next, the byte to be output is placed on the data bus
		/// and written to the selected peripheral device. Finally, the register pair HL is
		/// decremented.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if B–1 = 0; reset otherwise
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void OUTD()
		{
			OUTx();
			_Status.HL--;
		}


		/// <summary>
		/// The contents of the HL register pair are placed on the address bus to select a
		/// location in memory. The byte contained in this memory location is tempo-rarily
		/// stored in the CPU. Then, after the byte counter (B) is decremented,
		/// the contents of register C are placed on the bottom half (A0 through A7) of
		/// the address bus to select the I/O device at one of 256 possible ports. Regis-ter
		/// B may be used as a byte counter, and its decremented value is placed on
		/// the top half (A8 through A15) of the address bus at this time. Next, the byte
		/// to be output is placed on the data bus and written to the selected peripheral
		/// device. Then, register pair HL is decremented and if the decremented B
		/// register is not zero, the Program Counter (PC) is decremented by two and
		/// the instruction is repeated. If B has gone to zero, the instruction is termi-nated.
		/// Interrupts are recognized and two refresh cycles are executed after
		/// each data transfer.
		/// Note: When B is set to zero prior to instruction execution, the instruc-tion
		/// outputs 256 bytes of data.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void OTDR()
		{
			OUTx();
			_Status.HL--;
			if(_Status.B != 0) 
			{ 
				tstates += 5; 
				_Status.PC -= 2; 
			}
		}


		/// <summary>
		/// The contents of the HL register pair are placed on the address bus to select a
		/// location in memory. The byte contained in this memory location is
		/// temporarily stored in the CPU. Then, after the byte counter (B) is
		/// decremented, the contents of register C are placed on the bottom half (A0
		/// through A7) of the address bus to select the I/O device at one of 256
		/// possible ports. Register B may be used as a byte counter, and its
		/// decremented value is placed on the top half (A8 through A15) of the
		/// address bus. The byte to be output is placed on the data bus and written to a
		/// selected peripheral device. Finally, the register pair HL is incremented.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if B–1 = 0; reset otherwise
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void OUTI()
		{
			OUTx();
			_Status.HL++;
		}


		/// <summary>
		/// The contents of the HL register pair are placed on the address bus to select
		/// a location in memory. The byte contained in this memory location is tempo-rarily
		/// stored in the CPU. Then, after the byte counter (B) is decremented, the
		/// contents of register C are placed on the bottom half (A0 through A7) of the
		/// address bus to select the I/O device at one of 256 possible ports. Register B
		/// may be used as a byte counter, and its decremented value is placed on the top
		/// half (A8 through A15) of the address bus at this time. Next, the byte to be
		/// output is placed on the data bus and written to the selected peripheral device.
		/// Then register pair HL is incremented. If the decremented B register is not
		/// zero, the Program Counter (PC) is decremented by two and the instruction is
		/// repeated. If B has gone to zero, the instruction is terminated. Interrupts are
		/// recognized and two refresh cycles are executed after each data transfer.
		/// Note: When B is set to zero prior to instruction execution, the instruc-tion
		/// outputs 256 bytes of data.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void OTIR()
		{
			OUTx();
			_Status.HL++;
			if(_Status.B != 0) 
			{ 
				tstates += 5; 
				_Status.PC -= 2; 
			}
		}

		/// <summary>
		/// Used by OUTI, OUTD, OTIR, OTDR
		/// The contents of the HL register pair are placed on the address bus to select a
		/// location in memory. The byte contained in this memory location is
		/// temporarily stored in the CPU. Then, after the byte counter (B) is
		/// decremented, the contents of register C are placed on the bottom half (A0
		/// through A7) of the address bus to select the I/O device at one of 256
		/// possible ports. Register B may be used as a byte counter, and its
		/// decremented value is placed on the top half (A8 through A15) of the
		/// address bus. The byte to be output is placed on the data bus and written to a
		/// selected peripheral device. The register pair HL must be incremented or decremented
		/// by the callre.
		/// Condition Bits Affected:
		/// S is unknown
		/// Z is set if B–1 = 0; reset otherwise
		/// H is unknown
		/// P/V is unknown
		/// N is set
		/// C is not affected
		/// </summary>
		private void OUTx()
		{
			byte _b = _Memory.ReadByte(_Status.HL);
			_Status.B--;	
			_IO.WritePort(_Status.BC, _b);
			_Status.F = (byte) (((_b & 0x80) != 0 ? FlagRegisterDefinition.N : (byte) 0 ) | LookupTable_sz53[_Status.B]);
#warning S,H and P/V flags not implemented
		}



		/// <summary>
		/// The byte at the memory location specified by the contents of the Stack
		/// Pointer (SP) register pair is moved to the low order eight bits of the
		/// Program Counter (PC). The SP is now incremented and the byte at the
		/// memory location specified by the new contents of this instruction is fetched
		/// from the memory location specified by the PC. This instruction is normally
		/// used to return to the main line program at the completion of a routine
		/// entered by a CALL instruction.
		/// Condition Bits Affected: None
		/// </summary>
		private void RET()
		{
			ushort _PC;
			Pop(out _PC);
			_Status.PC = _PC;
		}

		/// <summary>
		/// The contents of the op operand are rotated left 1-bit position. The content of
		/// bit 7 is copied to the Carry flag and the previous content of the Carry flag is
		/// copied to bit 0.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity even; reset otherwise
		/// N is reset
		/// C is data from bit 7 of source register
		/// </summary>
		/// <param name="op">The operand</param>
		private void RL(HalfRegister op)
		{
			byte _op = op.Value;
			op.Value = (byte) (( op.Value<<1 ) | ( _Status.F & FlagRegisterDefinition.C ));
			_Status.F = (byte) (( _op >> 7 ) | LookupTable_sz53p[op.Value]);
		}

		/// <summary>
		/// The contents of the Accumulator (register A) are rotated left 1-bit position
		/// through the Carry flag. The previous content of the Carry flag is copied to
		/// bit 0. Bit 0 is the least-significant bit.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is not affected
		/// N is reset
		/// C is data from bit 7 of Accumulator
		/// </summary>
		private void RLA()
		{
			byte _A = _Status.A;
			_Status.A = (byte) (( _Status.A << 1 ) | ( _Status.F & FlagRegisterDefinition.C ));
			_Status.F = (byte) (
				( _Status.F & ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) | 
				( _Status.A & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) | 
				( _A >> 7 ));
		}

		/// <summary>
		/// The contents of the Accumulator (register A) are rotated left 1-bit position.
		/// The sign bit (bit 7) is copied to the Carry flag and also to bit 0. Bit 0 is the
		/// least-significant bit.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is not affected
		/// N is reset
		/// C is data from bit 7 of Accumulator
		/// </summary>
		private void RLCA()
		{
			_Status.A = (byte) (( _Status.A << 1 ) | ( _Status.A >> 7 ));
			_Status.F = (byte) (
				( _Status.F & ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) | 
				( _Status.A & ( FlagRegisterDefinition.C | FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ));
		}


		/// <summary>
		/// The contents of operand op are rotated left 1-bit position. The content of bit 7
		/// is copied to the Carry flag and also to bit 0.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity even; reset otherwise
		/// N is reset
		/// C is data from bit 7 of source register
		/// </summary>
		/// <param name="op"></param>
		private void RLC(HalfRegister op)
		{
			op.Value = (byte) (( op.Value << 1 ) | ( op.Value >> 7 ));
			_Status.F = (byte) (
				( op.Value & FlagRegisterDefinition.C ) | 
				LookupTable_sz53p[op.Value]);
		}


		/// <summary>
		/// The contents of the low order four bits (bits 3, 2, 1, and 0) of the memory
		/// location (HL) are copied to the high order four bits (7, 6, 5, and 4) of that
		/// same memory location; the previous contents of those high order four bits
		/// are copied to the low order four bits of the Accumulator (register A); and
		/// the previous contents of the low order four bits of the Accumulator are
		/// copied to the low order four bits of memory location (HL). The contents of
		/// the high order bits of the Accumulator are unaffected.
		/// Note: (HL) means the memory location specified by the contents of the
		/// HL register pair.
		/// Condition Bits Affected:
		/// S is set if Accumulator is negative after operation; reset otherwise
		/// Z is set if Accumulator is zero after operation; reset otherwise
		/// H is reset
		/// P/V is set if parity of Accumulator is even after operation; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		private void RLD()
		{
			byte _b = _Memory.ReadByte(_Status.HL);
			_Memory.WriteByte(_Status.HL, (byte)  ((_b << 4 ) | ( _Status.A & 0x0F )) );
			_Status.A = (byte) (( _Status.A & 0xF0 ) | ( _b >> 4 ));
			_Status.F = (byte) (( _Status.F & FlagRegisterDefinition.C ) | LookupTable_sz53p[_Status.A]);
		}



		/// <summary>
		/// The contents of operand op are rotated right 1-bit position through the Carry
		/// flag. The content of bit 0 is copied to the Carry flag and the previous
		/// content of the Carry flag is copied to bit 7. Bit 0 is the least-significant bit.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity even; reset otherwise,
		/// N is reset
		/// C is data from bit 0 of source register
		/// </summary>
		/// <param name="op">The operand</param>
		private void RR(HalfRegister op)
		{
			byte _op = op.Value;
			op.Value = (byte) (( op.Value>>1 ) | ( _Status.F << 7 ));
			_Status.F = (byte) (
				( _op & FlagRegisterDefinition.C ) | 
				LookupTable_sz53p[op.Value]);
		}

		/// <summary>
		/// The contents of the Accumulator (register A) are rotated right 1-bit position
		/// through the Carry flag. The previous content of the Carry flag is copied to
		/// bit 7. Bit 0 is the least-significant bit.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is not affected
		/// N is reset
		/// C is data from bit 0 of Accumulator
		/// </summary>
		private void RRA()
		{
			byte _A = _Status.A;
			_Status.A = (byte) (( _Status.A >> 1 ) | ( _Status.F << 7 ));
			_Status.F = (byte) (
				( _Status.F & ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) | 
				( _Status.A & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) ) | 
				( _A & FlagRegisterDefinition.C )) ;
		}


		/// <summary>
		/// The contents of the op operand are rotated right 1-bit position. The content
		/// of bit 0 is copied to the Carry flag and also to bit 7. Bit 0 is the least-significant
		/// bit.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity even; reset otherwise,
		/// N is reset
		/// C is data from bit 0 of source register
		/// </summary>
		/// <param name="op">The operand</param>
		private void RRC(HalfRegister op)
		{
			_Status.F = (byte) (op.Value & FlagRegisterDefinition.C);
			op.Value = (byte) (( op.Value >> 1 ) | ( op.Value << 7 ));
			_Status.F |= LookupTable_sz53p[op.Value];
		}

		/// <summary>
		/// The contents of the Accumulator (register A) are rotated right 1-bit
		/// position. Bit 0 is copied to the Carry flag and also to bit 7. Bit 0 is the leastsignificant
		/// bit.
		/// Condition Bits Affected:
		/// S is not affected
		/// Z is not affected
		/// H is reset
		/// P/V is not affected
		/// N is reset
		/// C is data from bit 0 of Accumulator
		/// </summary>
		private void RRCA()
		{
			_Status.F = (byte) (( _Status.F & ( FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S ) ) | ( _Status.A & FlagRegisterDefinition.C ));
			_Status.A = (byte) (( _Status.A >> 1) | ( _Status.A << 7 ));
			_Status.F |= (byte) ( _Status.A & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 ) );
		}


		/// <summary>
		/// The contents of the low order four bits (bits 3, 2, 1, and 0) of memory
		/// location (HL) are copied to the low order four bits of the Accumulator
		/// (register A). The previous contents of the low order four bits of the
		/// Accumulator are copied to the high order four bits (7, 6, 5, and 4) of
		/// location (HL); and the previous contents of the high order four bits of (HL)
		/// are copied to the low order four bits of (HL). The contents of the high order
		/// bits of the Accumulator are unaffected.
		/// (HL) means the memory location specified by the contents of the HL
		/// register pair.
		/// Condition Bits Affected:
		/// S is set if Accumulator is negative after operation; reset otherwise
		/// Z is set if Accumulator is zero after operation; reset otherwise
		/// H is reset
		/// P/V is set if parity of Accumulator is even after operation; reset otherwise
		/// N is reset
		/// C is not affected
		/// </summary>
		private void RRD()
		{
			byte _b = _Memory.ReadByte(_Status.HL);
			_Memory.WriteByte(_Status.HL, (byte) (( _Status.A << 4 ) | ( _b >> 4 )));
			_Status.A = (byte) (( _Status.A & 0xF0 ) | ( _b & 0x0F ));
			_Status.F = (byte) (( _Status.F & FlagRegisterDefinition.C ) | LookupTable_sz53p[_Status.A]);
		}


		/// <summary>
		/// The current Program Counter (PC) contents are pushed onto the external
		/// memory stack, and the page zero memory location given by operand op is
		/// loaded to the PC. Program execution then begins with the Op Code in the
		/// address now pointed to by PC. The push is performed by first decrementing
		/// the contents of the Stack Pointer (SP), loading the high-order byte of PC to
		/// the memory address now pointed to by SP, decrementing SP again, and
		/// loading the low order byte of PC to the address now pointed to by SP. The
		/// Restart instruction allows for a jump to one of eight addresses indicated in
		/// the table below. The operand op is assembled to the object code using the
		/// corresponding T state.
		/// Because all addresses are in page zero of memory, the high order byte of
		/// PC is loaded with 00H. The number selected from the p column of the table
		/// is loaded to the low order byte of PC.
		/// p   t
		/// 00H 000
		/// 08H 001
		/// 10H 010
		/// 18H 011
		/// 20H 100
		/// 28H 101
		/// 30H 110
		/// 38H 111
		/// </summary>
		/// <param name="op">The operand</param>
		private void RST(byte op)
		{
			Push(_Status.PC);
			_Status.PC = op;
		}

		/// <summary>
		/// The s operand, along with the Carry flag (C in the F register) is subtracted
		/// from the contents of the Accumulator, and the result is stored in the
		/// Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is reset if overflow; reset otherwise
		/// N is set
		/// C is set if borrow; reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void SBC_A(byte op)
		{
			ushort result = (ushort) (_Status.A - op - ( _Status.F & FlagRegisterDefinition.C ));
			byte lookup = (byte) (( (_Status.A & 0x88) >> 3 ) | ( ( op & 0x88 ) >> 2 ) | ( (result & 0x88) >> 1 ));
			_Status.A = (byte) result;
			_Status.F = (byte) (( (result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte) 0 ) | FlagRegisterDefinition.N | LookupTable_halfcarry_sub[lookup & 0x07] | LookupTable_overflow_sub[lookup >> 4] | LookupTable_sz53[_Status.A]);
		}

		/// <summary>
		/// The contents of the operand op and the Carry Flag (C flag in the F register) 
		/// are subtracted from the contents of register pair HL, and the result is 
		/// stored in HL.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if a borrow from bit 12; reset otherwise
		/// P/V is set if overflow; reset otherwise
		/// N is set
		/// C is set if borrow; reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void SBC_HL(ushort op)
		{
			uint result = (uint) (_Status.HL - op - ((_Status.F & FlagRegisterDefinition.C) != 0 ? 1 : 0));
			byte lookup = (byte) ((byte) ( ( _Status.HL & 0x8800 ) >> 11 ) | (byte) ( ( op & 0x8800 ) >> 10 ) | (byte) ( ( result & 0x8800 ) >> 9 ));
			_Status.HL = (ushort) result;
			_Status.F = (byte) (( (result & 0x10000) != 0 ? FlagRegisterDefinition.C : (byte) 0 ) | FlagRegisterDefinition.N | LookupTable_overflow_sub [lookup >> 4] | ( _Status.H & ( FlagRegisterDefinition._3 | FlagRegisterDefinition._5 | FlagRegisterDefinition.S ) ) | LookupTable_halfcarry_sub[lookup & 0x07] | ( _Status.HL != 0 ? (byte) 0 : FlagRegisterDefinition.Z) );
		}

		/// <summary>
		/// An arithmetic shift left 1-bit position is performed on the contents of
		/// operand op. The content of bit 7 is copied to the Carry flag. Bit 0 is the
		/// least-significant bit.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity is even; reset otherwise
		/// N is reset
		/// C is data from bit 7
		/// </summary>
		/// <param name="op">The operand</param>
		private void SLA(HalfRegister op)
		{
			_Status.F = (byte) (op.Value >> 7);
			op.Value <<= 1;
			_Status.F |= LookupTable_sz53p[op.Value];
		}

		/// <summary>
		/// An arithmetic shift left 1-bit position is performed on the contents of
		/// operand op. The content of bit 7 is copied to the Carry flag. Bit 0 is the
		/// least-significant bit.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity is even; reset otherwise
		/// N is reset
		/// C is data from bit 7
		/// </summary>
		/// <param name="op">The operand</param>
		private void SLL(HalfRegister op)
		{
			_Status.F = (byte) (op.Value >> 7);
			op.Value = (byte) (( op.Value << 1 ) | 0x01);
			_Status.F |= LookupTable_sz53p[op.Value];
		}

		/// <summary>
		/// An arithmetic shift right 1-bit position is performed on the contents of
		/// operand m. The content of bit 0 is copied to the Carry flag and the previous
		/// content of bit 7 is unchanged. Bit 0 is the least-significant bit.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity is even; reset otherwise
		/// N is reset
		/// C is data from bit 0 of source register
		/// </summary>
		/// <param name="op">The operand</param>
		private void SRA(HalfRegister op)
		{
			_Status.F = (byte) (op.Value & FlagRegisterDefinition.C);
			op.Value = (byte) (( op.Value & 0x80 ) | ( op.Value >> 1 ));
			_Status.F |= LookupTable_sz53p[op.Value];
		}

		/// <summary>
		/// The contents of operand op are shifted right 1-bit position. The content of
		/// bit 0 is copied to the Carry flag, and bit 7 is reset. Bit 0 is the least-significant
		/// bit.
		/// Condition Bits Affected:
		/// S is reset
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity is even; reset otherwise
		/// N is reset
		/// C is data from bit 0 of source register
		/// </summary>
		/// <param name="op">The operand</param>
		private void SRL(HalfRegister op)
		{
			_Status.F = (byte) (op.Value & FlagRegisterDefinition.C);
			op.Value >>= 1;
			_Status.F |= LookupTable_sz53p[op.Value];
		}

		/// <summary>
		/// The s operand is subtracted from the contents of the Accumulator, and the
		/// result is stored in the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is set if borrow from bit 4; reset otherwise
		/// P/V is set if overflow; reset otherwise
		/// N is set
		/// C is set if borrow; reset otherwise
		/// </summary>
		/// <param name="op">The operand</param>
		private void SUB(byte op)
		{
			ushort result = (ushort) (_Status.A - op);
			byte lookup = (byte) (( (_Status.A & 0x88) >> 3 ) | ( ( op & 0x88 ) >> 2 ) | ( (result & 0x88) >> 1 ));
			_Status.A = (byte) result;
			_Status.F = (byte) (( (result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte) 0 ) | FlagRegisterDefinition.N | LookupTable_halfcarry_sub[lookup & 0x07] | LookupTable_overflow_sub[lookup >> 4] | LookupTable_sz53[_Status.A]);
		}

		/// <summary>
		/// The logical exclusive-OR operation is performed between the byte
		/// specified by the op operand and the byte contained in the Accumulator; the
		/// result is stored in the Accumulator.
		/// Condition Bits Affected:
		/// S is set if result is negative; reset otherwise
		/// Z is set if result is zero; reset otherwise
		/// H is reset
		/// P/V is set if parity even; reset otherwise
		/// N is reset
		/// C is reset
		/// </summary>
		/// <param name="op">The operand</param>
		private void XOR(byte op)
		{
			_Status.A ^= op;
			_Status.F = LookupTable_sz53p[_Status.A];
		}




		#endregion

		#region Execution unit


		/// <summary>
		/// Number of statements to fetch before returning (-1) if no return at all.
		/// It should be used for debug purpose (set to -1 after each fetch).
		/// </summary>
		public int StatementsToFetch
		{
			get
			{
				return _StatementsToFetch;
			}
			set
			{
				_StatementsToFetch = value;
			}
		}



		/// <summary>
		/// Main execution
		/// </summary>
		public void Execute()
		{
			ushort Address;
			

			while(tstates < event_next_event ) 
			{

				byte opcode;

				// Check if Statement to fetch must be handled
				if (StatementsToFetch >= 0)
				{
					if (StatementsToFetch == 0)
					{
						// Disable next break (just in case the main program forget to do it)
						StatementsToFetch = -1;
						return;
					}
					else
						StatementsToFetch--;
				}

				// Check if someone is registered to receive Fetch event and eventually raise it
				if (OnFetch != null)
					OnFetch();


				// If the z80 is HALTed, execute a NOP-equivalent and loop again
				if(_Status.Halted) 
				{
					tstates+=4;
					continue;
				}

				// Fetch next instruction
				opcode=_Memory.ReadByte(_Status.PC++);
				
				// Increment refresh register
				_Status.R++;
/*
#warning Da eliminare
				System.IO.TextWriter tw = new System.IO.StreamWriter("c:\\speccy\\CSEState.txt",true);
				_Status.Serialize(tw);
				tw.Close();
*/

				
				if (opcode == 0x76)		// HALT
				{
					// The first check is for HALT otherwise it could be
					// interpreted as LD (HL),(HL)
					tstates += 4;
					_Status.Halted = true;
				}
				else if ((opcode & 0xC0) == 0x40)	// LD r,r
				{
					HalfRegister reg1 = GetHalfRegister((byte) (opcode >> 3));
					HalfRegister reg2 = GetHalfRegister(opcode);

					if (reg1 == null)
					{
						// The target is (HL)
						tstates += 7;
						_Memory.WriteByte(_Status.HL, reg2.Value);
					}
					else if (reg2 == null)
					{
						// The source is (HL)
						tstates += 7;
						reg1.Value = _Memory.ReadByte(_Status.HL);
					}
					else
					{
						// Source and target are normal registries
						tstates += 4;
						reg1.Value = reg2.Value;
					}
				}
				else if ((opcode & 0xC0) == 0x80)
				{
					// Operation beetween accumulator and other registers
					// Usually are identified by 10 ooo rrr where ooo is the operation and rrr is the source register
					HalfRegister reg = GetHalfRegister(opcode);
					byte _Value;

					if (reg == null)
					{
						// The source is (HL)
						tstates += 7;
						_Value = _Memory.ReadByte(_Status.HL);
					}
					else
					{
						// The source is a normal registry
						tstates += 4;
						_Value = reg.Value;
					}

					switch (opcode & 0xF8)
					{
						case 0x80:	// ADD A,r
							ADD_A(_Value);
							break;
						case 0x88:	// ADC A,r
							ADC_A(_Value);
							break;
						case 0x90:	// SUB r
							SUB(_Value);
							break;
						case 0x98:	// SBC A,r
							SBC_A(_Value);
							break;
						case 0xA0:	// AND r
							AND_A(_Value);
							break;
						case 0xA8:	// XOR r
							XOR(_Value);
							break;
						case 0xB0:  // OR r
							OR(_Value);
							break;
						case 0xB8:	// CP r
							CP(_Value);
							break;
						default:
							throw new Exception("Wrong place in the right time...");
					}

				}
				else if ((opcode & 0xC7) == 0x04) // INC r
				{
					HalfRegister reg = GetHalfRegister((byte) (opcode >> 3));

					if (reg == null)
					{
						// The target is (HL)
						tstates += 7;
						reg = new HalfRegister(_Memory.ReadByte(_Status.HL));
						INC(reg);
						_Memory.WriteByte(_Status.HL, reg.Value);
					}
					else
					{
						// The target is a normal registry
						tstates += 4;
						INC(reg);
					}
				}
				else if ((opcode & 0xC7) == 0x05) // DEC r
				{
					HalfRegister reg = GetHalfRegister((byte) (opcode >> 3));

					if (reg == null)
					{
						// The target is (HL)
						tstates += 7;
						reg = new HalfRegister(_Memory.ReadByte(_Status.HL));
						DEC(reg);
						_Memory.WriteByte(_Status.HL, reg.Value);
					}
					else
					{
						// The target is a normal registry
						tstates += 4;
						DEC(reg);
					}
				}
				else if ((opcode & 0xC7) == 0x06) // LD r,nn
				{
					HalfRegister reg = GetHalfRegister((byte) (opcode >> 3));
					byte Value = _Memory.ReadByte(_Status.PC++);

					if (reg == null)
					{
						// The target is (HL)
						tstates += 10;
						_Memory.WriteByte(_Status.HL, Value);
					}
					else
					{
						// The target is a normal registry
						tstates += 7;
						reg.Value = Value;
					}
				}
				else if ((opcode & 0xC7) == 0xC0) // RET cc
				{
					tstates += 5;
					if (opcode == 0xC0 && Status.PC == 0x056C)
					{
						if (tape_load_trap() == 0)
							break;
					}
					if (CheckFlag(opcode))
					{
						tstates += 6;
						RET();
					}
				}
				else if ((opcode & 0xC7) == 0xC2) // JP cc,nn
				{
					tstates += 10;
					if (CheckFlag(opcode))
						JP();
					else
						_Status.PC += 2;
				}
				else if ((opcode & 0xC7) == 0xC4) // CALL cc,nn
				{
					tstates += 10;
					if (CheckFlag(opcode))
					{
						tstates += 7;
						CALL();
					}
					else
						_Status.PC += 2;
				}
				else if ((opcode & 0xC7) == 0xC7) // RST p
				{
					tstates += 11;
					RST((byte) (opcode & 0x38));
				}
				else if ((opcode & 0xCF) == 0x01) // LD dd,nn
				{
					tstates += 10;
					Register reg = GetRegister(opcode, true);
					ushort Value = _Memory.ReadWord(Status.PC);
					Status.PC += 2;

					reg.w = Value;
				}
				else if ((opcode & 0xCF) == 0x03) // INC ss
				{
					tstates += 6;
					Register reg = GetRegister(opcode, true);
					
					// No flags affected
					reg.w++;
				}
				else if ((opcode & 0xCF) == 0x09) // ADD HL,ss
				{
					tstates += 11;
					Register reg = GetRegister(opcode, true);

					ADD_16(_Status.RegisterHL, reg.w);
				}
				else if ((opcode & 0xCF) == 0x0B) // DEC ss
				{
					tstates += 6;
					Register reg = GetRegister(opcode, true);

					reg.w--;
				}
				else if ((opcode & 0xCF) == 0xC5) // PUSH qq
				{
					tstates += 11;
					Register reg = GetRegister(opcode, false);

					Push(reg);
				}

				else if ((opcode & 0xCF) == 0xC1) // POP qq
				{
					tstates += 10;
					Register reg = GetRegister(opcode, false);

					Pop(reg);
				}
				else
				{
					switch(opcode) 
					{
						case 0x00:		// NOP
							tstates += 4;
							break;
						case 0x02:		// LD (BC),A
							tstates += 7;
							_Memory.WriteByte(_Status.BC, _Status.A);
							break;
						case 0x07:		// RLCA
							tstates += 4;
							RLCA();
							break;
						case 0x08:		// EX AF,AF'
							tstates += 4;
							// The 2-byte contents of the register pairs AF and AF are exchanged.
							// Register pair AF consists of registers A' and F'.


							// Tape saving trap: note this traps the EX AF,AF' at #04d0, not #04d1 as PC has already been incremented 
							if( _Status.PC == 0x04d1 ) 
							{
								if( tape_save_trap() == 0 )
									break;
							}
							_Status.RegisterAF.Swap(_Status.RegisterAF_);
							break;
						case 0x0A:		// LD A,(BC)
							tstates += 7;
							_Status.A = _Memory.ReadByte(_Status.BC);
							break;
						
						case 0x0F:		// RRCA
							tstates += 4;
							RRCA();
							break;
						case 0x10:		// DJNZ offset
							tstates += 8;
							_Status.B--;
							if(_Status.B != 0)
							{ 
								tstates += 5;
								JR(); 
							}
							_Status.PC++;
							break;
						case 0x12:		// LD (DE),A
							tstates += 7;
							_Memory.WriteByte(_Status.DE,_Status.A);
							break;
						case 0x17:		// RLA
							tstates += 4;
							RLA();
							break;
						case 0x18:		// JR offset
							tstates += 12;
							JR();
							_Status.PC++;
							break;
						case 0x1A:		// LD A,(DE)
							tstates += 7;
							_Status.A = _Memory.ReadByte(_Status.DE);
							break;
						case 0x1F:		// RRA
							tstates += 4;
							RRA();
							break;
						case 0x20:		// JR NZ,offset
							tstates += 7;
							if (( _Status.F & FlagRegisterDefinition.Z ) == 0 ) 
							{ 
								tstates += 5; 
								JR(); 
							}
							_Status.PC++;
							break;
						case 0x22:		// LD (nnnn),HL
							tstates += 16;
							LD_nndd(_Status.RegisterHL);
							break;
						case 0x27:		// DAA
							tstates += 4;
							DAA();
							break;
						case 0x28:		// JR Z,offset
							tstates += 7;
							if ((_Status.F & FlagRegisterDefinition.Z) != 0 ) 
							{ 
								tstates += 5; 
								JR(); 
							}
							_Status.PC++;
							break;
						case 0x2A:		// LD HL,(nnnn)
							tstates += 16;
							LD_ddnn(_Status.RegisterHL);
							break;
						case 0x2F:		// CPL
							tstates += 4;
							CPL();
							break;
						case 0x30:		// JR NC,offset
							tstates += 7;
							if ((_Status.F & FlagRegisterDefinition.C) == 0 ) 
							{ 
								tstates += 5; 
								JR(); 
							}
							_Status.PC++;
							break;
						case 0x32:		// LD (nnnn),A
							tstates += 13;
							Address = _Memory.ReadWord(_Status.PC);
							_Status.PC += 2;
							_Memory.WriteByte(Address, _Status.A);
							break;
						case 0x37:		// SCF
							tstates += 4;
							_Status.F |= FlagRegisterDefinition.C;
							break;
						case 0x38:		// JR C,offset
							tstates += 7;
							if ((_Status.F & FlagRegisterDefinition.C) != 0) 
							{ 
								tstates += 5; 
								JR(); 
							}
							_Status.PC++;
							break;
						case 0x3A:		// LD A,(nnnn)
							tstates += 13;
							Address =_Memory.ReadWord(_Status.PC);
							_Status.PC += 2;
							_Status.A = _Memory.ReadByte(Address);
							break;
						case 0x3F:		// CCF
							tstates += 4;
							CCF();
							break;
						case 0xC3:		// JP nnnn
							tstates += 10;
							JP();
							break;
						case 0xC6:		// ADD A,nn
							tstates += 7;
							ADD_A(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xC9:		// RET
							tstates += 10;
							RET();
							break;
						case 0xCB:		// CBxx opcodes
							_Status.R++;
							Execute_CB(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xCD:		// CALL nnnn
							tstates += 17;
							CALL();
							break;
						case 0xCE:		// ADC A,nn
							tstates += 7;
							ADC_A(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xD3:		// OUT (nn),A
							tstates += 11;
							// The operand n is placed on the bottom half (A0 through A7) of the address
							// bus to select the I/O device at one of 256 possible ports. The contents of the
							// Accumulator (register A) also appear on the top half (A8 through A15) of
							// the address bus at this time. Then the byte contained in the Accumulator is
							// placed on the data bus and written to the selected peripheral device.
							_IO.WritePort( (ushort) (_Memory.ReadByte(_Status.PC++) | ( _Status.A << 8 )) , _Status.A);
							break;
						case 0xD6:		// SUB nn
							tstates += 7;
							SUB(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xD9:		// EXX
							tstates += 4;
							// Each 2-byte value in register pairs BC, DE, and HL is exchanged with the
							// 2-byte value in BC', DE', and HL', respectively.

							_Status.RegisterBC.Swap(Status.RegisterBC_);
							_Status.RegisterDE.Swap(Status.RegisterDE_);
							_Status.RegisterHL.Swap(Status.RegisterHL_);
							break;
						case 0xDB:		// IN A,(nn)
							tstates += 11;
							// The operand n is placed on the bottom half (A0 through A7) of the address
							// bus to select the I/O device at one of 256 possible ports. The contents of the
							// Accumulator also appear on the top half (A8 through A15) of the address
							// bus at this time. Then one byte from the selected port is placed on the data
							// bus and written to the Accumulator (register A) in the CPU.
							_Status.A= _IO.ReadPort( (ushort) (_Memory.ReadByte(_Status.PC++) | ( _Status.A << 8 )));
							break;

						
						
						case 0xDD:		// DDxx opcodes
							_Status.R++;
							Execute_DDFD(_Status.RegisterIX, _Memory.ReadByte(_Status.PC++));
							break;
						case 0xDE:		// SBC A,nn
							tstates += 4;
							SBC_A(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xE3:		// EX (SP),HL
							tstates += 19;
						{
							ushort _w = _Memory.ReadWord(_Status.SP);
							_Memory.WriteWord(_Status.SP, _Status.HL);
							_Status.HL = _w;
						}
							break;
						case 0xE6:		// AND nn
							tstates += 7;
							AND_A(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xE9:		// JP HL
							tstates += 4;
							_Status.PC = _Status.HL;
							break;
						case 0xEB:		// EX DE,HL
							tstates += 4;
							_Status.RegisterDE.Swap(_Status.RegisterHL);
							break;

						
						case 0xed:		// EDxx opcodes
							_Status.R++;
							Execute_ED(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xEE:		// XOR A,nn
							tstates += 7;
							XOR(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xF3:		// DI
							tstates += 4;
							// DI disables the maskable interrupt by resetting the interrupt enable flip-flops
							// (IFF1 and IFF2). Note that this instruction disables the maskable
							// interrupt during its execution.
							_Status.IFF1 = false;
							_Status.IFF2 = false;
							break;
						case 0xF6:		// OR nn
							tstates += 7;
							OR(_Memory.ReadByte(_Status.PC++));
							break;
						case 0xF9:		// LD SP,HL
							tstates += 6;
							_Status.SP = _Status.HL;
							break;
						case 0xFB:		// EI
							tstates += 4;
							// The enable interrupt instruction sets both interrupt enable flip flops (IFF1
							// and IFF2) to a logic 1, allowing recognition of any maskable interrupt. Note
							// that during the execution of this instruction and the following instruction,
							// maskable interrupts are disabled.
							_Status.IFF1 = true;
							_Status.IFF2 = true;
							break;
						case 0xFD:		// FDxx opcodes
							_Status.R++;
							Execute_DDFD(_Status.RegisterIY, _Memory.ReadByte(_Status.PC++));
							break;
						case 0xFE:		// CP nn
							tstates += 7;
							CP(_Memory.ReadByte(_Status.PC++));
							break;
						default:
							throw new Exception(string.Format("Internal execute error. Opcode {0} not implemented.", opcode));


					}
				}
			}
		}

		/// <summary>
		/// Execution of DD xx codes and FD xx codes.
		/// DD and FD prefix change a multiplexer from HL to IX (if prefix is DD) or IY (if prefix is FD)
		/// </summary>
		/// <param name="RegisterI_">It must be IX if previous opcode was DD, IY if previous opcode was FD</param>
		/// <param name="opcode">opcode to execute</param>
		private void Execute_DDFD(Register RegisterI_, byte opcode)
		{

			// Opcodes are the same as base opcodes but HL is substituted with IX or IY and (HL)
			// is substituted with (IX + d) or (IY + d).
			/*
			
			0x09	 9	00001001	 ADD REGISTER,BC 
			0x19	25	00011001	 ADD REGISTER,DE 
			0x21	33	00100001	 LD REGISTER,nnnn 
			0x22	34	00100010	 LD (nnnn),REGISTER 
			0x23	35	00100011	 INC REGISTER 
			0x24	36	00100100	 INC REGISTERH 
			0x25	37	00100101	 DEC REGISTERH 
			0x26	38	00100110	 LD REGISTERH,nn 
			0x29	41	00101001	 ADD REGISTER,REGISTER 
			0x2a	42	00101010	 LD REGISTER,(nnnn) 
			0x2b	43	00101011	 DEC REGISTER 
			0x2c	44	00101100	 INC REGISTERL 
			0x2d	45	00101101	 DEC REGISTERL 
			0x2e	46	00101110	 LD REGISTERL,nn 
			0x34	52	00110100	 INC (REGISTER + d) 
			0x35	53	00110101	 DEC (REGISTER + d) 
			0x36	54	00110110	 LD (REGISTER + d),nn 
			0x39	57	00111001	 ADD REGISTER,SP 
			0x44	68	01000100	 LD B,REGISTERH 
			0x45	69	01000101	 LD B,REGISTERL 
			0x46	70	01000110	 LD B,(REGISTER + d) 
			0x4c	76	01001100	 LD C,REGISTERH 
			0x4d	77	01001101	 LD C,REGISTERL 
			0x4e	78	01001110	 LD C,(REGISTER + d) 
			0x54	84	01010100	 LD D,REGISTERH 
			0x55	85	01010101	 LD D,REGISTERL 
			0x56	86	01010110	 LD D,(REGISTER + d) 
			0x5c	92	01011100	 LD E,REGISTERH 
			0x5d	93	01011101	 LD E,REGISTERL 
			0x5e	94	01011110	 LD E,(REGISTER + d) 
			0x60	96	01100000	 LD REGISTERH,B 
			0x61	97	01100001	 LD REGISTERH,C 
			0x62	98	01100010	 LD REGISTERH,D 
			0x63	99	01100011	 LD REGISTERH,E 
			0x64	100	01100100	 LD REGISTERH,REGISTERH 
			0x65	101	01100101	 LD REGISTERH,REGISTERL 
			0x66	102	01100110	 LD H,(REGISTER + d) 
			0x67	103	01100111	 LD REGISTERH,A 
			0x68	104	01101000	 LD REGISTERL,B 
			0x69	105	01101001	 LD REGISTERL,C 
			0x6a	106	01101010	 LD REGISTERL,D 
			0x6b	107	01101011	 LD REGISTERL,E 
			0x6c	108	01101100	 LD REGISTERL,REGISTERH 
			0x6d	109	01101101	 LD REGISTERL,REGISTERL 
			0x6e	110	01101110	 LD L,(REGISTER + d) 
			0x6f	111	01101111	 LD REGISTERL,A 
			0x70	112	01110000	 LD (REGISTER + d),B 
			0x71	113	01110001	 LD (REGISTER + d),C 
			0x72	114	01110010	 LD (REGISTER + d),D 
			0x73	115	01110011	 LD (REGISTER + d),E 
			0x74	116	01110100	 LD (REGISTER + d),H 
			0x75	117	01110101	 LD (REGISTER + d),L 
			0x77	119	01110111	 LD (REGISTER + d),A 
			0x7c	124	01111100	 LD A,REGISTERH 
			0x7d	125	01111101	 LD A,REGISTERL 
			0x7e	126	01111110	 LD A,(REGISTER + d) 
			0x84	132	10000100	 ADD A,REGISTERH 
			0x85	133	10000101	 ADD A,REGISTERL 
			0x86	134	10000110	 ADD A,(REGISTER + d) 
			0x8c	140	10001100	 ADC A,REGISTERH 
			0x8d	141	10001101	 ADC A,REGISTERL 
			0x8e	142	10001110	 ADC A,(REGISTER + d) 
			0x94	148	10010100	 SUB A,REGISTERH 
			0x95	149	10010101	 SUB A,REGISTERL 
			0x96	150	10010110	 SUB A,(REGISTER + d) 
			0x9c	156	10011100	 SBC A,REGISTERH 
			0x9d	157	10011101	 SBC A,REGISTERL 
			0x9e	158	10011110	 SBC A,(REGISTER + d) 
			0xa4	164	10100100	 AND A,REGISTERH 
			0xa5	165	10100101	 AND A,REGISTERL 
			0xa6	166	10100110	 AND A,(REGISTER + d) 
			0xac	172	10101100	 XOR A,REGISTERH 
			0xad	173	10101101	 XOR A,REGISTERL 
			0xae	174	10101110	 XOR A,(REGISTER + d) 
			0xb4	180	10110100	 OR A,REGISTERH 
			0xb5	181	10110101	 OR A,REGISTERL 
			0xb6	182	10110110	 OR A,(REGISTER + d) 
			0xbc	188	10111100	 CP A,REGISTERH 
			0xbd	189	10111101	 CP A,REGISTERL 
			0xbe	190	10111110	 CP A,(REGISTER + d) 
			0xcb	203	11001011	 {DD|FD}CBxx opcodes 
			0xe1	225	11100001	 POP REGISTER 
			0xe3	227	11100011	 EX (SP),REGISTER 
			0xe5	229	11100101	 PUSH REGISTER 
			0xe9	233	11101001	 JP REGISTER 
			0xf9	249	11111001	 LD SP,REGISTER 
			*/






			// I* registers are used for indexed access so (I*) will be used often
			HalfRegister _I__;
			ushort Address;

			if (opcode == 0x76)		// HALT
			{
				// The first check is for HALT otherwise it could be
				// interpreted as LD (I_ + d),(I_ + d)
				tstates += 4;
				_Status.Halted = true;
			}
			else if ((opcode & 0xC0) == 0x40)	// LD r,r'
			{
				HalfRegister reg1 = GetHalfRegister((byte) (opcode >> 3));
				HalfRegister reg2 = GetHalfRegister(opcode);

				if (reg1 == null)
				{
					// The target is (I_ + d)
					tstates += 19;
					Address = (ushort) (RegisterI_.w + (sbyte) _Memory.ReadByte(_Status.PC++));
					_Memory.WriteByte(Address, reg2.Value);
				}
				else if (reg2 == null)
				{
					// The source is (I_ + d)
					tstates += 19;
					Address = (ushort) (RegisterI_.w + (sbyte) _Memory.ReadByte(_Status.PC++));
					reg1.Value = _Memory.ReadByte(Address);
				}
				else
				{
					// Source and target are normal registers but HL is now substituted by I_
					if (reg1 == _Status.RegisterHL.h)
						reg1 = RegisterI_.h;
					if (reg1 == Status.RegisterHL.l)
						reg1 = RegisterI_.l;

					if (reg2 == _Status.RegisterHL.h)
						reg2 = RegisterI_.h;
					if (reg2 == Status.RegisterHL.l)
						reg2 = RegisterI_.l;

					tstates += 8;
					reg1.Value = reg2.Value;
				}
			}
			else if ((opcode & 0xC0) == 0x80)
			{
				// Operation beetween accumulator and other registers
				// Usually are identified by 10 ooo rrr where ooo is the operation and rrr is the source register
				HalfRegister reg = GetHalfRegister(opcode);
				byte _Value;

				if (reg == null)
				{
					// The source is (I_ + d)
					tstates += 19;
					_Value = _Memory.ReadByte((ushort) (RegisterI_.w + (sbyte) _Memory.ReadByte(_Status.PC++)));
				}
				else
				{
					// The source is a normal registry but HL is substituted by I_
					tstates += 8;
					if (reg == _Status.RegisterHL.h)
						_Value = RegisterI_.h.Value;
					else if (reg == _Status.RegisterHL.l)
						_Value = RegisterI_.l.Value;
					else
						_Value = reg.Value;
				}

				switch (opcode & 0xF8)
				{
					case 0x80:	// ADD A,r
						ADD_A(_Value);
						break;
					case 0x88:	// ADC A,r
						ADC_A(_Value);
						break;
					case 0x90:	// SUB r
						SUB(_Value);
						break;
					case 0x98:	// SBC A,r
						SBC_A(_Value);
						break;
					case 0xA0:	// AND r
						AND_A(_Value);
						break;
					case 0xA8:	// XOR r
						XOR(_Value);
						break;
					case 0xB0:  // OR r
						OR(_Value);
						break;
					case 0xB8:	// CP r
						CP(_Value);
						break;
					default:
						throw new Exception("Wrong place in the right time...");
				}

			}
			else
			{

				switch (opcode)
				{
					case 0x09:		// ADD I_,BC
						tstates += 15;
						ADD_16(RegisterI_, _Status.BC);
						break;

					case 0x19:		// ADD I_,DE
						tstates += 15;
						ADD_16(RegisterI_, _Status.DE);
						break;

					case 0x21:		// LD I_,nnnn
						tstates += 14;
						RegisterI_.w = _Memory.ReadWord(_Status.PC);
						_Status.PC += 2;
						break;

					case 0x22:		// LD (nnnn),I_
						tstates += 20;
						LD_nndd(RegisterI_);
						break;

					case 0x23:		// INC I_
						tstates += 10;
						RegisterI_.w++;
						break;

					case 0x24:		// INC I_.h
						tstates += 8;
						INC(RegisterI_.h);
						break;

					case 0x25:		// DEC I_.h
						tstates += 8;
						DEC(RegisterI_.h);
						break;

					case 0x26:		// LD I_.h,nn
						tstates += 11;
						RegisterI_.h.Value = _Memory.ReadByte(_Status.PC++);
						break;

					case 0x29:		// ADD I_,I_
						tstates += 15;
						ADD_16(RegisterI_, RegisterI_.w);
						break;

					case 0x2A:		// LD I_,(nnnn)
						tstates += 20;
						LD_ddnn(RegisterI_);
						break;

					case 0x2B:		// DEC I_
						tstates += 10;
						RegisterI_.w--;
						break;

					case 0x2C:		// INC I_.l
						tstates += 8;
						INC(RegisterI_.l);
						break;

					case 0x2D:		// DEC I_.l
						tstates += 8;
						DEC(RegisterI_.l);
						break;

					case 0x2E:		// LD I_.l,nn
						tstates += 11;
						RegisterI_.l.Value = _Memory.ReadByte(_Status.PC++);
						break;

					case 0x34:		// INC (I_ + d)
						tstates += 23;
						Address = (ushort) (RegisterI_.w + (sbyte) _Memory.ReadByte(_Status.PC++));
						_I__ = new HalfRegister(_Memory.ReadByte(Address));
						INC(_I__);
						_Memory.WriteByte(Address, _I__.Value);
						break;

					case 0x35:		// DEC (I_ + d)
						tstates += 23;
						Address = (ushort) (RegisterI_.w + (sbyte) _Memory.ReadByte(_Status.PC++));
						_I__ = new HalfRegister(_Memory.ReadByte(Address));
						INC(_I__);
						_Memory.WriteByte(Address, _I__.Value);
						break;

					case 0x36:		// LD (I_ + d),nn
						tstates += 19;
						Address = (ushort) (RegisterI_.w + (sbyte) _Memory.ReadByte(_Status.PC++));
						_Memory.WriteByte(Address, _Memory.ReadByte(_Status.PC++));
						break;

					case 0x39:		// ADD I_,SP
						tstates += 15;
						ADD_16(RegisterI_, Status.SP);
						break;




					case 0xCB:		// {DD|FD}CBxx opcodes
					{
						Address = (ushort) (RegisterI_.w + (sbyte)_Memory.ReadByte(_Status.PC++));
						Execute_DDFD_CB(Address, _Memory.ReadByte(_Status.PC++));
					}
						break;
					case 0xE1:		// POP I_
						tstates += 14;
						Pop(RegisterI_);
						break;

					case 0xE3:		// EX (SP),I_
						tstates += 23;
					{
						ushort _w = _Memory.ReadWord(_Status.SP);
						_Memory.WriteWord(_Status.SP, RegisterI_.w);
						RegisterI_.w = _w;
					}
						break;
					case 0xE5:		// PUSH I_
						tstates += 15;
						Push(RegisterI_);
						break;

					case 0xE9:		// JP I_
						tstates += 8;
						_Status.PC = RegisterI_.w;
						break;

						// Note EB (EX DE,HL) does not get modified to use either IX or IY;
						// this is because all EX DE,HL does is switch an internal flip-flop
						// in the Z80 which says which way round DE and HL are, which can't
						// be used with IX or IY. (This is also why EX DE,HL is very quick
						// at only 4 T states).

					case 0xF9:		// LD SP,I_
						tstates += 10;
						_Status.SP = RegisterI_.w;
						break;

					default:		
						// Instruction did not involve H or L, so backtrack one instruction and parse again
						tstates += 4;
						_Status.PC--;
						break;

				}
			}
		}


		/// <summary>
		/// Execution of ED xx codes
		/// </summary>
		/// <param name="opcode">opcode to execute</param>
		private void Execute_ED(byte opcode)
		{


			if ((opcode & 0xC7) == 0x40) // IN r,(C)
			{
				HalfRegister reg = GetHalfRegister((byte) (opcode >> 3));

				// In this case 110 does not write in (HL) but affects only the flags
				if (reg == null)
					reg = new HalfRegister();

				tstates += 12;
				IN(reg, _Status.BC);
			}
			else if ((opcode & 0xC7) == 0x41) // OUT (C),r
			{
				// The contents of register C are placed on the bottom half (A0 through A7) of
				// the address bus to select the I/O device at one of 256 possible ports. The
				// contents of Register B are placed on the top half (A8 through A15) of the
				// address bus at this time. Then the byte contained in register r is placed on
				// the data bus and written to the selected peripheral device.
				HalfRegister reg = GetHalfRegister((byte) (opcode >> 3));

				// In this case 110 outputs 0 in out port
				if (reg == null)
					reg = new HalfRegister(0);

				tstates += 12;
				_IO.WritePort(_Status.BC, reg.Value);
			}
			else if ((opcode & 0xC7) == 0x42) // ALU operations with HL
			{
				tstates += 15;
				Register reg = GetRegister(opcode, true);
				switch (opcode & 0x08)
				{
					case 0: // SBC HL,ss
						SBC_HL(reg.w);
						break;
					case 8: // ADC HL,ss
						ADC_HL(reg.w);
						break;
					default:
						throw new Exception("No no no!!!");
				}
			}
			else if ((opcode & 0xC7) == 0x43) // Load register from to memory address
			{
				tstates += 20;
				Register reg = GetRegister(opcode, true);
				switch (opcode & 0x08)
				{
					case 0: // LD (nnnn),ss
						LD_nndd(reg);
						break;
					case 8: // LD ss,(nnnn)
						LD_ddnn(reg);
						break;
					default:
						throw new Exception("No no no!!!");
				}

			}
			else
			{
				
				switch (opcode)
				{


					case 0x44:	
					case 0x4c:
					case 0x54:
					case 0x5c:
					case 0x64:
					case 0x6c:
					case 0x74:
					case 0x7c:	// NEG
						// The contents of the Accumulator are negated (two’s complement). This is
						// the same as subtracting the contents of the Accumulator from zero. Note
						// that 80H is left unchanged.
						// Condition Bits Affected:
						// S is set if result is negative; reset otherwise
						// Z is set if result is 0; reset otherwise
						// H is set if borrow from bit 4; reset otherwise
						// P/V is set if Accumulator was 80H before operation; reset otherwise
						// N is set
						// C is set if Accumulator was not 00H before operation; reset otherwise
						tstates += 8;
					{
						byte _b = _Status.A;
						_Status.A = 0;
						SUB(_b);
					}
						break;

					case 0x45:
					case 0x4d:			// RETI
						// This instruction is used at the end of a maskable interrupt service routine to:
						// • Restore the contents of the Program Counter (PC) (analogous to the
						// RET instruction)
						// • Signal an I/O device that the interrupt routine is completed. The RETI
						// instruction also facilitates the nesting of interrupts, allowing higher
						// priority devices to temporarily suspend service of lower priority
						// service routines. However, this instruction does not enable interrupts
						// that were disabled when the interrupt routine was entered. Before
						// doing the RETI instruction, the enable interrupt instruction (EI)
						// should be executed to allow recognition of interrupts after completion
						// of the current service routine.

						// TODO: Reading Z80 specs this instruction should not copy IFF2 to IFF1 but
						// in real Z80 seems that the operation is done.
					case 0x55:
					case 0x5d:
					case 0x65:
					case 0x6d:
					case 0x75:
					case 0x7d:      // RETN
						// This instruction is used at the end of a non-maskable interrupts service
						// routine to restore the contents of the Program Counter (PC) (analogous to
						// the RET instruction). The state of IFF2 is copied back to IFF1 so that
						// maskable interrupts are enabled immediately following the RETN if they
						// were enabled before the nonmaskable interrupt.
						tstates  +=  14;
						_Status.IFF1 = _Status.IFF2;
						RET();
						break;

					case 0x46:
					case 0x4e:
					case 0x66:
					case 0x6e:	// IM 0
						// The IM 0 instruction sets interrupt mode 0. In this mode, the interrupting
						// device can insert any instruction on the data bus for execution by the
						// CPU. The first byte of a multi-byte instruction is read during the interrupt
						// acknowledge cycle. Subsequent bytes are read in by a normal memory
						// read sequence.
						tstates += 8;
						_Status.IM = 0;
						break;

					case 0x47:	// LD I,A
						tstates += 9;
						_Status.I = _Status.A;
						break;

					case 0x4F:	// LD R,A
						tstates += 9;
						_Status.R = _Status.R7 = _Status.A;
						break;

					case 0x56:
					case 0x76:	// IM 1
						tstates += 8;
						// The IM 1 instruction sets interrupt mode 1. In this mode, the processor
						// responds to an interrupt by executing a restart to location 0038H.
						_Status.IM = 1;
						break;
					case 0x57:	// LD A,I
						tstates += 9;
						// The contents of the Interrupt Vector Register I are loaded to the Accumulator.
						// Condition Bits Affected:
						// S is set if I-Register is negative; reset otherwise
						// Z is set if I-Register is zero; reset otherwise
						// H is reset
						// P/V contains contents of IFF2
						// N is reset
						// C is not affected
						// If an interrupt occurs during execution of this instruction, the Parity
						// flag contains a 0.
						_Status.A = _Status.I;
						_Status.F = (byte) (( _Status.F & FlagRegisterDefinition.C) | LookupTable_sz53[_Status.A] | ( _Status.IFF2 ? FlagRegisterDefinition.V : (byte) 0 ));
						break;
					case 0x5E:
					case 0x7E:	// IM 2
						// The IM 2 instruction sets the vectored interrupt mode 2. This mode allows
						// an indirect call to any memory location by an 8-bit vector supplied from the
						// peripheral device. This vector then becomes the least-significant eight bits
						// of the indirect pointer, while the I register in the CPU provides the most-significant
						// eight bits. This address points to an address in a vector table that
						// is the starting address for the interrupt service routine.
						tstates += 8;
						_Status.IM = 2;
						break;

					case 0x5F:	// LD A,R
						tstates += 9;
						_Status.A = (byte) ((_Status.R & 0x7F) | (_Status.R7 & 0x80));
						_Status.F = (byte) (( _Status.F & FlagRegisterDefinition.C ) | LookupTable_sz53[_Status.A] | ( _Status.IFF2 ? FlagRegisterDefinition.V : (byte) 0 ));
						break;

					case 0x67:	// RRD
						tstates += 18;
						RRD();
						break;
					case 0x6F:	// RLD
						tstates += 18;
						RLD();
						break;
					case 0xA0:	// LDI
						tstates += 16;
						LDI();
						break;
					case 0xA1:	// CPI
						tstates += 16;
						CPI();
						break;

					case 0xA2:	// INI
						tstates += 16;
						INI();
						break;

					case 0xA3:	// OUTI
						tstates += 16;
						OUTI();
						break;

					case 0xA8:	// LDD
						tstates += 16;
						LDD();
						break;

					case 0xA9:	// CPD
						tstates += 16;
						CPD();
						break;
					case 0xAA:	// IND
						tstates += 16;
						IND();
						break;

					case 0xAB:	// OUTD
						tstates += 16;
						OUTD();
						break;

					case 0xB0:	// LDIR
						tstates += 16;
						LDIR();
						break;

					case 0xB1:	// CPIR
						tstates += 16;
						CPIR();
						break;
				  
					case 0xB2:	// INIR
						tstates += 16;
						INIR();
						break;

					case 0xB3:	// OTIR
						tstates += 16;
						OTIR();
						break;

					case 0xB8:	// LDDR
						tstates += 17;
						LDDR();
						break;

					case 0xB9:	// CPDR
						tstates += 16;
						CPDR();
						break;

					case 0xba:	// INDR
						tstates += 16;
						INDR();
						break;

					case 0xbb:	// OTDR
						tstates += 16;
						OTDR();
						break;

					default:	// All other opcodes are NOPD
						tstates += 8;
						break;

				}
			}
		}


		
		/// <summary>
		/// Execution of CB xx codes
		/// </summary>
		/// <param name="opcode">opcode to execute</param>
		private void Execute_CB(byte opcode)
		{


			// Operations with single byte register
			// The format is 00 ooo rrr where ooo is the operation and rrr is the register
			HalfRegister reg = GetHalfRegister(opcode);
			HalfRegister _HL_ = null;

			// Check if the source/target is (HL)
			if (reg == null)
				reg = _HL_ = new HalfRegister(_Memory.ReadByte(_Status.HL));

			Execute_CB_on_reg(opcode, reg);

			if (reg == _HL_)
			{
				// The target is (HL)
				tstates += 15;
				_Memory.WriteByte(_Status.HL, _HL_.Value); //We should not do this when we check bits (BIT n,r)!
			}
			else
			{
				// The source is a normal registry
				tstates += 8;
			}


		}


		/// <summary>
		/// Execution of DD CB xx codes or FD CB xx codes
		/// </summary>
		/// <param name="Address">Address to act on - Address = I_ + d</param>
		/// <param name="opcode">opcode</param>
		private void Execute_DDFD_CB(ushort Address, byte opcode)
		{
			// This is a mix of DD/FD opcodes (Normal operation but access to 
			// I_ register instead of HL register) and CB op codes.
			// Behaviour is a little different:
			// if (Opcodes use B, C, D, E, H, L)  -  opcodes with rrr different from 110
			//   r = (I_ + d)
			//   execute_op r
			//   (I_ + d) = r
			// if (Opcodes use (HL))              -  opcodes with rrr = 110
			//   execute_op (I_ + d)
			//
			// if execute_op is a bit checking operation BIT n,r no assignement are done



			HalfRegister reg;

			// Check if the operation is a bit checking operation
			// The format is 01 bbb rrr
			if (opcode >> 6 == 0x01)
			{
				reg = new HalfRegister();
				tstates += 20;
			}
			else
			{
				// Retrieve the register from opcode xxxxx rrr
				reg = GetHalfRegister(opcode);

				// Check if the source is (I_ + d) so the op will not act on any register
				// but only on memory
				if (reg == null)
				{
					tstates += 23;
					reg = new HalfRegister();
				}
				else
#warning Check tstates (See next comment)
					tstates += 23;
				// In case reg is not null the timings are not documented. I think the operation 
				// take at least 23 tstates (the same of the operation without storing the 
				// result in register too).
			}

			// Assign (I_ + d) value to reg
			reg.Value = _Memory.ReadByte(Address);


			Execute_CB_on_reg(opcode, reg);

			_Memory.WriteByte(Address, reg.Value);

		}


		/// <summary>
		/// This is the low level function called within a CB opcode fetch
		/// (single byte or DD CB or FD CB)
		/// It must be called after the execution unit has determined on
		/// wich register act
		/// </summary>
		/// <param name="opcode">opcode</param>
		/// <param name="reg">Register to act on</param>
		private void Execute_CB_on_reg(byte opcode, HalfRegister reg)
		{
			switch (opcode >> 3)
			{
				case 0:	// RLC r
					RLC(reg);
					break;
				case 1:	// RRC r
					RRC(reg);
					break;
				case 2: // RL r
					RL(reg);
					break;
				case 3: // RR r
					RR(reg);
					break;
				case 4: // SLA r
					SLA(reg);
					break;
				case 5: // SRA r
					SRA(reg);
					break;
				case 6: // SLL r
					SLL(reg);
					break;
				case 7: // SRL r
					SRL(reg);
					break;
				default:
					// Work on bits
					
					// The format is oo bbb rrr
					// oo is the operation (01 BIT, 10 RES, 11 SET)
					// bbb is the bit number
					// rrr is the register
					byte bit = (byte) ((opcode >> 3) & 0x07);

				switch (opcode >> 6)
				{
					case 1: // BIT n,r
						if (bit == 7)
							BIT7(reg.Value);
						else
							BIT(bit, reg.Value);
						break;
					case 2: // RES n,r
						reg.Value &= (byte) ~(1 << bit);
						break;
					case 3: // SET n,r
						reg.Value |= (byte) (1<< bit);
						break;
					default:
						throw new Exception("What am I doing here?!?");
				}

					break;
			}
		}

#warning Questi li ho messi per farlo partire in fretta
		public int event_next_event = 69888;
		int tape_load_trap(){return 0;}
		int tape_save_trap(){return 0;}
			

		#endregion
	}
}
