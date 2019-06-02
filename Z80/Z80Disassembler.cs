using System;

namespace Z80
{
	/// <summary>
	/// Disassembler for Z80
	/// </summary>
	public class Z80Disassembler
	{

		/// <summary>
		/// Preferred show format
		/// </summary>
		public enum ShowFormat
		{
			/// <summary>
			/// Hexadecimal format (0x_)
			/// </summary>
			Hex = 0,
			/// <summary>
			/// Decimal format (_)
			/// </summary>
			Decimal = 1,
            Octal = 2
		}

		// Show data format
		private ShowFormat _ShowDataFormat = ShowFormat.Octal;

		// Memory 
		private IMemory _Memory;


		/// <summary>
		/// Create a new Z80 disassembler
		/// </summary>
		public Z80Disassembler()
		{

		}

		/// <summary>
		/// Create new Z80 disassembler
		/// </summary>
		/// <param name="Memory">Memory to debug</param>
		public Z80Disassembler(IMemory Memory)
		{
			_Memory = Memory;
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
			set
			{
				_Memory = value;
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
		/// <returns>The half register.</returns>
		private string GetHalfRegister(byte opcode)
		{
			switch (opcode & 0x07)
			{
				case 0x00:
					return "B";
				case 0x01:
					return "C";
				case 0x02:
					return "D";
				case 0x03:
					return "E";
				case 0x04:
					return "H";
				case 0x05:
					return "L";
				case 0x06:
					return "(HL)";
				case 0x07:
					return "A";
			}
			throw new Exception("Why am I here?");
		}

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
		/// <param name="realHL">real HL (it can be HL, IX, IY)</param>
		/// <param name="OpCodeAddress">Address of next opcode. It's used if half register is (IX + d) or is (IY + d)</param>
		/// <returns>The half register.</returns>
		private string GetHalfRegister(byte opcode, string realHL, ref ushort OpCodeAddress)
		{
			switch (opcode & 0x07)
			{
				case 0x00:
					return "B";
				case 0x01:
					return "C";
				case 0x02:
					return "D";
				case 0x03:
					return "E";
				case 0x04:
					return realHL + ".H";
				case 0x05:
					return realHL + ".L";
				case 0x06:
					return string.Format("({0} + {1})", realHL, (sbyte) _Memory.ReadByte(OpCodeAddress++));
				case 0x07:
					return "A";
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
		/// <param name="ReturnSP">Checked if opcode is xx11xxxx. If this parameter is true then SP is returned else AF is returned</param>
		/// <returns>The register</returns>
		private string GetRegister(byte opcode, bool ReturnSP)
		{
			switch (opcode & 0x30)
			{
				case 0x00:
					return "BC";
				case 0x10:
					return "DE";
				case 0x20:
					return "HL";
				case 0x30:
					if (ReturnSP)
						return "SP";
					else
						return "AF";
			}
			throw new Exception("What's happening to me?");
		}


		/// <summary>
		/// Return the flag related to opcode according with the following table:
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
		/// <returns>The flag in mnemonic form</returns>
		private string CheckFlag(byte opcode)
		{
			// Find the right flag and the condition
			switch ((opcode >> 3) & 0x07)
			{
				case 0:
					return "NZ";
				case 1:
					return "Z";
				case 2:
					return "NC";
				case 3:
					return "C";
				case 4:
					return "PO";
				case 5:
					return "PE";
				case 6:
					return "P";
				case 7:
					return "M";
				default:
					throw new Exception("I'm feeling bad");
			}
		}

		private string ShowData(int v)
		{
			if (_ShowDataFormat == ShowFormat.Decimal)
				return v.ToString();
            else if (_ShowDataFormat == ShowFormat.Hex)
                return v.ToString("X");
            else
            {
                if (v > 255)
                {
                    int x;
                    x = (v / 256);
                    int d3 = (x % 8);
                    x = x / 8;
                    int d4 = (x % 8);
                    x = x / 8;
                    int d5 = (x % 8);
                    x = (v % 256);
                    int d0 = (x % 8);
                    x = x / 8;
                    int d1 = (x % 8);
                    x = x / 8;
                    int d2 = (x % 8);
                    return (string.Format("{0}{1}{2}.{3}{4}{5}", d5, d4, d3, d2, d1, d0));
                }
                else
                {
                    int x;
                    int d0 = v % 8;
                    x = v / 8;
                    int d1 = x % 8;
                    x = x / 8;
                    int d2 = x % 8;
                    return (string.Format("{0}{1}{2}", d2, d1, d0));
                }
            }
		}

		#endregion


		#region Execution unit

		/// <summary>
		/// Main execution
		/// </summary>
		public string Disassemble(ref ushort OpCodeAddress)
		{
			ushort Address;
			ushort IOAddress;
			byte opcode;
			ShowFormat DataFormat = ShowFormat.Decimal;

			// Fetch next instruction
			opcode = _Memory.ReadByte(OpCodeAddress++);
		
		
			if (opcode == 0x76)		// HALT
			{
				// The first check is for HALT otherwise it could be
				// interpreted as LD (HL),(HL)
				return "HALT";
			}
			else if ((opcode & 0xC0) == 0x40)	// LD r,r
			{
				string reg1 = GetHalfRegister((byte) (opcode >> 3));
				string reg2 = GetHalfRegister(opcode);

				return string.Format("LD {0},{1}", reg1, reg2);
			}
			else if ((opcode & 0xC0) == 0x80)
			{
				// Operation beetween accumulator and other registers
				// Usually are identified by 10 ooo rrr where ooo is the operation and rrr is the source register
				string reg = GetHalfRegister(opcode);

				switch (opcode & 0xF8)
				{
					case 0x80:	// ADD A,r
						return string.Format("ADD A,{0}", reg);
					case 0x88:	// ADC A,r
						return string.Format("ADC A,{0}", reg);
					case 0x90:	// SUB r
						return string.Format("SUB {0}", reg);
					case 0x98:	// SBC A,r
						return string.Format("SBC A,{0}", reg);
					case 0xA0:	// AND r
						return string.Format("AND {0}", reg);
					case 0xA8:	// XOR r
						return string.Format("XOR {0}", reg);
					case 0xB0:  // OR r
						return string.Format("OR {0}", reg);
					case 0xB8:	// CP r
						return string.Format("CP {0}", reg);
					default:
						throw new Exception("Wrong place in the right time...");
				}

			}
			else if ((opcode & 0xC7) == 0x04) // INC r
			{
				string reg = GetHalfRegister((byte) (opcode >> 3));
				return string.Format("INC {0}", reg);
			}
			else if ((opcode & 0xC7) == 0x05) // DEC r
			{
				string reg = GetHalfRegister((byte) (opcode >> 3));
				return string.Format("DEC {0}", reg);
			}
			else if ((opcode & 0xC7) == 0x06) // LD r,nn
			{
				string reg = GetHalfRegister((byte) (opcode >> 3));
				byte Value = _Memory.ReadByte(OpCodeAddress++);
				return string.Format("LD {0},{1}", reg, ShowData(Value));
			}
			else if ((opcode & 0xC7) == 0xC0) // RET cc
			{
				return string.Format("RET {0}", CheckFlag(opcode));
			}
			else if ((opcode & 0xC7) == 0xC2) // JP cc,nn
			{
				Address = _Memory.ReadWord(OpCodeAddress);
				string retValue = string.Format("JP {0},{1}", CheckFlag(opcode), ShowData(Address));
				OpCodeAddress += 2;
				return retValue;
			}
			else if ((opcode & 0xC7) == 0xC4) // CALL cc,nn
			{
				Address = _Memory.ReadWord(OpCodeAddress);
				string retValue = string.Format("JP {0},{1}", CheckFlag(opcode), ShowData(Address));
				OpCodeAddress += 2;
				return retValue;
			}
			else if ((opcode & 0xC7) == 0xC7) // RST p
			{
				return string.Format("RST {0}", ShowData((opcode & 0x38)));
			}
			else if ((opcode & 0xCF) == 0x01) // LD dd,nn
			{
				string reg = GetRegister(opcode, true);
				ushort Value = _Memory.ReadWord(OpCodeAddress);
				OpCodeAddress += 2;
				return string.Format("LD {0},{1}", reg, ShowData(Value));
			}
			else if ((opcode & 0xCF) == 0x03) // INC ss
			{
				string reg = GetRegister(opcode, true);
				return string.Format("INC {0}", reg);
			}
			else if ((opcode & 0xCF) == 0x09) // ADD HL,ss
			{
				string reg = GetRegister(opcode, true);
				return string.Format("ADD HL,{0}", reg);
			}
			else if ((opcode & 0xCF) == 0x0B) // DEC ss
			{
				string reg = GetRegister(opcode, true);
				return string.Format("DEC {0}", reg);
			}
			else if ((opcode & 0xCF) == 0xC5) // PUSH qq
			{
				string reg = GetRegister(opcode, false);
				return string.Format("PUSH {0}", reg);
			}

			else if ((opcode & 0xCF) == 0xC1) // POP qq
			{
				string reg = GetRegister(opcode, false);
				return string.Format("POP {0}", reg);
			}
			else
			{
				switch(opcode) 
				{
					case 0x00:		// NOP
						return "NOP";
					case 0x02:		// LD (BC),A
						return "LD (BC),A";
					case 0x07:		// RLCA
						return "RLCA";
					case 0x08:		// EX AF,AF'
						return "EX AF,AF'";
					case 0x0A:		// LD A,(BC)
						return "LD A,(BC)";
					case 0x0F:		// RRCA
						return "RRCA";
					case 0x10:		// DJNZ offset
						return string.Format("DJNZ {0}", ShowData((sbyte) _Memory.ReadByte(OpCodeAddress++) + OpCodeAddress));
					case 0x12:		// LD (DE),A
						return "LD (DE),A";
					case 0x17:		// RLA
						return "RLA";
					case 0x18:		// JR offset
						return string.Format("JR {0}", ShowData((sbyte) _Memory.ReadByte(OpCodeAddress++) + OpCodeAddress));
					case 0x1A:		// LD A,(DE)
						return "LD A,(DE)";
					case 0x1F:		// RRA
						return "RRA";
					case 0x20:		// JR NZ,offset
						return string.Format("JR NZ,{0}", ShowData((sbyte) _Memory.ReadByte(OpCodeAddress++) + OpCodeAddress));
					case 0x22:		// LD (nnnn),HL
						Address = _Memory.ReadWord(OpCodeAddress);
						OpCodeAddress += 2;
						return string.Format("LD ({0}),HL", ShowData(Address));
					case 0x27:		// DAA
						return "DAA";
					case 0x28:		// JR Z,offset
						return string.Format("JR Z,{0}", ShowData((sbyte) _Memory.ReadByte(OpCodeAddress++) + OpCodeAddress));
					case 0x2A:		// LD HL,(nnnn)
						Address = _Memory.ReadWord(OpCodeAddress);
						OpCodeAddress += 2;
						return string.Format("LD HL,({0})", ShowData(Address));
					case 0x2F:		// CPL
						return "CPL";
					case 0x30:		// JR NC,offset
						return string.Format("JR NC,{0}", ShowData((sbyte) _Memory.ReadByte(OpCodeAddress++) + OpCodeAddress));
					case 0x32:		// LD (nnnn),A
						Address = _Memory.ReadWord(OpCodeAddress);
						OpCodeAddress += 2;
						return string.Format("LD ({0}),A", ShowData(Address));
					case 0x37:		// SCF
						return "SCF";
					case 0x38:		// JR C,offset
						return string.Format("JR C,{0}", ShowData((sbyte) _Memory.ReadByte(OpCodeAddress++) + OpCodeAddress));
					case 0x3A:		// LD A,(nnnn)
						Address = _Memory.ReadWord(OpCodeAddress);
						OpCodeAddress += 2;
						return string.Format("LD A,({0})", ShowData(Address));
					case 0x3F:		// CCF
						return "CCF";
					case 0xC3:		// JP nnnn
						Address = _Memory.ReadWord(OpCodeAddress);
						OpCodeAddress += 2;
						return string.Format("JP {0}", ShowData(Address));
					case 0xC6:		// ADD A,nn
						return string.Format("ADD A,{0}", ShowData(_Memory.ReadByte(OpCodeAddress++)));
					case 0xC9:		// RET
						return "RET";
					case 0xCB:		// CBxx opcodes
						return Disassemble_CB(_Memory.ReadByte(OpCodeAddress++));
					case 0xCD:		// CALL nnnn
						Address = _Memory.ReadWord(OpCodeAddress);
						OpCodeAddress += 2;
						return string.Format("CALL {0}", ShowData(Address));
					case 0xCE:		// ADC A,nn
						return string.Format("ADC A,{0}", ShowData(_Memory.ReadByte(OpCodeAddress++)));
					case 0xD3:		// OUT (nn),A
						IOAddress = _Memory.ReadByte(OpCodeAddress++);
						return string.Format("OUT ({0}),A", ShowData(IOAddress));
					case 0xD6:		// SUB nn
						return string.Format("SUB {0}", _Memory.ReadByte(OpCodeAddress++));
					case 0xD9:		// EXX
						// Each 2-byte value in register pairs BC, DE, and HL is exchanged with the
						// 2-byte value in BC', DE', and HL', respectively.
						return "EXX";
					case 0xDB:		// IN A,(nn)
						// The operand n is placed on the bottom half (A0 through A7) of the address
						// bus to select the I/O device at one of 256 possible ports. The contents of the
						// Accumulator also appear on the top half (A8 through A15) of the address
						// bus at this time. Then one byte from the selected port is placed on the data
						// bus and written to the Accumulator (register A) in the CPU.
						IOAddress = _Memory.ReadByte(OpCodeAddress++);
						return string.Format("IN A,({0})", ShowData(IOAddress));
					case 0xDD:		// DDxx opcodes
						return Disassemble_DDFD("IX", ref OpCodeAddress);
					case 0xDE:		// SBC A,nn
						return string.Format("SBC A,{0}", _Memory.ReadByte(OpCodeAddress++));
					case 0xE3:		// EX (SP),HL
						return "EX (SP),HL";
					case 0xE6:		// AND nn
						return string.Format("AND {0}", _Memory.ReadByte(OpCodeAddress++));
					case 0xE9:		// JP HL
						return "JP HL";
					case 0xEB:		// EX DE,HL
						return "EX DE,HL";
					case 0xed:		// EDxx opcodes
						return Disassemble_ED(ref OpCodeAddress);
					case 0xEE:		// XOR A,nn
						return string.Format("XOR A,{0}", _Memory.ReadByte(OpCodeAddress++));
					case 0xF3:		// DI
						return "DI";
					case 0xF6:		// OR nn
						return string.Format("OR {0}", _Memory.ReadByte(OpCodeAddress++));
					case 0xF9:		// LD SP,HL
						return "LD SP,HL";
					case 0xFB:		// EI
						return "EI";
					case 0xFD:		// FDxx opcodes
						return Disassemble_DDFD("IY", ref OpCodeAddress);
					case 0xFE:		// CP nn
						return string.Format("CP {0}", _Memory.ReadByte(OpCodeAddress++));
					default:
						throw new Exception(string.Format("Internal disassembler error. Opcode {0} not implemented.", opcode));

				}
			}
		}

		/// <summary>
		/// Execution of DD xx codes and FD xx codes.
		/// DD and FD prefix change a multiplexer from HL to IX (if prefix is DD) or IY (if prefix is FD)
		/// </summary>
		/// <param name="RegisterI_">It must be IX if previous opcode was DD, IY if previous opcode was FD</param>
		/// <param name="OpCodeAddress">Address of next opcode</param>
		private string Disassemble_DDFD(string RegisterI_, ref ushort OpCodeAddress)
		{

			byte opcode = _Memory.ReadByte(OpCodeAddress++);

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





			string retVal;

			if (opcode == 0x76)		// HALT
			{
				// The first check is for HALT otherwise it could be
				// interpreted as LD (I_ + d),(I_ + d)
				return "HALT";
			}
			else if ((opcode & 0xC0) == 0x40)	// LD r,r'
			{
				string reg1 = GetHalfRegister((byte) (opcode >> 3), RegisterI_, ref OpCodeAddress);
				string reg2 = GetHalfRegister(opcode, RegisterI_, ref OpCodeAddress);

				return string.Format("LD {0},{1}", reg1, reg2);
			}
			else if ((opcode & 0xC0) == 0x80)
			{
				// Operation beetween accumulator and other registers
				// Usually are identified by 10 ooo rrr where ooo is the operation and rrr is the source register
				string reg = GetHalfRegister(opcode, RegisterI_, ref OpCodeAddress);

				switch (opcode & 0xF8)
				{
					case 0x80:	// ADD A,r
						return string.Format("ADD A,{0}", reg);
					case 0x88:	// ADC A,r
						return string.Format("ADC A,{0}", reg);
					case 0x90:	// SUB r
						return string.Format("SUB {0}", reg);
					case 0x98:	// SBC A,r
						return string.Format("SBC A,{0}", reg);
					case 0xA0:	// AND r
						return string.Format("AND {0}", reg);
					case 0xA8:	// XOR r
						return string.Format("XOR {0}", reg);
					case 0xB0:  // OR r
						return string.Format("OR {0}", reg);
					case 0xB8:	// CP r
						return string.Format("CP {0}", reg);
					default:
						throw new Exception("Wrong place in the right time...");
				}
			}
			else
			{

				switch (opcode)
				{
					case 0x09:		// ADD I_,BC
						return string.Format("ADD {0},BC", RegisterI_);
					case 0x19:		// ADD I_,DE
						return string.Format("ADD {0},DE", RegisterI_);
					case 0x21:		// LD I_,nnnn
						retVal = string.Format("LD {0},{1}", RegisterI_, ShowData(_Memory.ReadWord(OpCodeAddress)));
						OpCodeAddress += 2;
						return retVal;
					case 0x22:		// LD (nnnn),I_
						retVal = string.Format("LD ({0}),{1}", ShowData(_Memory.ReadWord(OpCodeAddress)), RegisterI_);
						OpCodeAddress += 2;
						return retVal;
					case 0x23:		// INC I_
						return string.Format("INC {0}", RegisterI_);
					case 0x24:		// INC I_.h
						return string.Format("INC {0}.H", RegisterI_);
					case 0x25:		// DEC I_.h
						return string.Format("DEC {0}.H", RegisterI_);
					case 0x26:		// LD I_.h,nn
						return string.Format("LD {0}.H,{1}", RegisterI_, _Memory.ReadByte(OpCodeAddress++));
					case 0x29:		// ADD I_,I_
						return string.Format("ADD {0},{0}");
					case 0x2A:		// LD I_,(nnnn)
						retVal = string.Format("LD {0},({1})", RegisterI_, ShowData(_Memory.ReadWord(OpCodeAddress)));
						OpCodeAddress += 2;
						return retVal;
					case 0x2B:		// DEC I_
						return string.Format("DEC {0}", RegisterI_);
					case 0x2C:		// INC I_.l
						return string.Format("INC {0}.L", RegisterI_);
					case 0x2D:		// DEC I_.l
						return string.Format("DEC {0}.L", RegisterI_);
					case 0x2E:		// LD I_.l,nn
						return string.Format("LD {0}.L,{1}", RegisterI_, _Memory.ReadByte(OpCodeAddress++));
					case 0x34:		// INC (I_ + d)
						return string.Format("INC {0}", GetHalfRegister((byte)(opcode >> 3), RegisterI_, ref OpCodeAddress));
					case 0x35:		// DEC (I_ + d)
						return string.Format("DEC {0}", GetHalfRegister((byte)(opcode >> 3), RegisterI_, ref OpCodeAddress));
					case 0x36:		// LD (I_ + d),nn
						// Hope everything is going to be evaluated in the right way (from left to right)
						return string.Format("LD {0},{1}", GetHalfRegister(opcode, RegisterI_, ref OpCodeAddress), _Memory.ReadByte(OpCodeAddress++));
					case 0x39:		// ADD I_,SP
						return string.Format("ADD {0},SP", RegisterI_);
					case 0xCB:		// {DD|FD}CBxx opcodes
						// Still Hoping everything is going to be evaluated in the right way (from left to right)
						return Disassemble_DDFD_CB(string.Format("({0} + {1})",RegisterI_ ,(sbyte)_Memory.ReadByte(OpCodeAddress++)), _Memory.ReadByte(OpCodeAddress++));
					case 0xE1:		// POP I_
						return string.Format("POP {0}", RegisterI_);
					case 0xE3:		// EX (SP),I_
						return string.Format("EX SP,{0}", RegisterI_);
					case 0xE5:		// PUSH I_
						return string.Format("PUSH {0}", RegisterI_);
					case 0xE9:		// JP I_
						return string.Format("JP {0}", RegisterI_);

						// Note EB (EX DE,HL) does not get modified to use either IX or IY;
						// this is because all EX DE,HL does is switch an internal flip-flop
						// in the Z80 which says which way round DE and HL are, which can't
						// be used with IX or IY. (This is also why EX DE,HL is very quick
						// at only 4 T states).

					case 0xF9:		// LD SP,I_
						return string.Format("LD SP,{0}", RegisterI_);
					default:		
						// Instruction did not involve H or L, so backtrack one instruction and parse again
						OpCodeAddress--;
						return Disassemble(ref OpCodeAddress);
				}
			}
		}


		/// <summary>
		/// Execution of ED xx codes
		/// </summary>
		/// <param name="opcode">opcode to execute</param>
		private string Disassemble_ED(ref ushort OpCodeAddress)
		{
			byte opcode = _Memory.ReadByte(OpCodeAddress++);

			if ((opcode & 0xC7) == 0x40) // IN r,(C)
			{
				string reg = GetHalfRegister((byte) (opcode >> 3));

				return string.Format("IN {0},(C)", reg);
			}
			else if ((opcode & 0xC7) == 0x41) // OUT (C),r
			{
				// The contents of register C are placed on the bottom half (A0 through A7) of
				// the address bus to select the I/O device at one of 256 possible ports. The
				// contents of Register B are placed on the top half (A8 through A15) of the
				// address bus at this time. Then the byte contained in register r is placed on
				// the data bus and written to the selected peripheral device.
				string reg = GetHalfRegister((byte) (opcode >> 3));

				return string.Format("OUT (C),{0}", reg);
			}
			else if ((opcode & 0xC7) == 0x42) // ALU operations with HL
			{
				string reg = GetRegister(opcode, true);
				switch (opcode & 0x08)
				{
					case 0: // SBC HL,ss
						return string.Format("SBC HL,{0}", reg);
					case 8: // ADC HL,ss
						return string.Format("ADC HL,{0}", reg);
					default:
						throw new Exception("No no no!!!");
				}
			}
			else if ((opcode & 0xC7) == 0x43) // Load register from to memory address
			{
				string reg = GetRegister(opcode, true);
				string retVal;

				switch (opcode & 0x08)
				{
					case 0: // LD (nnnn),ss
						retVal = string.Format("LD ({0}),{1}", _Memory.ReadWord(OpCodeAddress), reg);
						break;
					case 8: // LD ss,(nnnn)
						retVal = string.Format("LD {1},({0})", _Memory.ReadWord(OpCodeAddress), reg);
						break;
					default:
						throw new Exception("No no no!!!");
				}
				OpCodeAddress += 2;
				return retVal;

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
						return "NEG";
					case 0x45:
					case 0x4d:
					case 0x55:
					case 0x5d:
					case 0x65:
					case 0x6d:
					case 0x75:
					case 0x7d:      // RETN
						return "RETN";
					case 0x46:
					case 0x4e:
					case 0x66:
					case 0x6e:	// IM 0
						return "IM 0";
					case 0x47:	// LD I,A
						return "LD I,A";
					case 0x4F:	// LD R,A
						return "LD R,A";
					case 0x56:
					case 0x76:	// IM 1
						return "IM 1";
					case 0x57:	// LD A,I
						return "LD A,I";
					case 0x5E:
					case 0x7E:	// IM 2
						return "IM 2";
					case 0x5F:	// LD A,R
						return "LD A,R";
					case 0x67:	// RRD
						return "RRD";
					case 0x6F:	// RLD
						return "RRD";
					case 0xA0:	// LDI
						return "LDI";
					case 0xA1:	// CPI
						return "CPI";
					case 0xA2:	// INI
						return "INI";
					case 0xA3:	// OUTI
						return "OUTI";
					case 0xA8:	// LDD
						return "LDD";
					case 0xA9:	// CPD
						return "CPD";
					case 0xAA:	// IND
						return "IND";
					case 0xAB:	// OUTD
						return "OUTD";
					case 0xB0:	// LDIR
						return "LDIR";
					case 0xB1:	// CPIR
						return "CPIR";
					case 0xB2:	// INIR
						return "INIR";
					case 0xB3:	// OTIR
						return "OTIR";
					case 0xB8:	// LDDR
						return "LDDR";
					case 0xB9:	// CPDR
						return "CPDR";
					case 0xBA:	// INDR
						return "INDR";
					case 0xBB:	// OTDR
						return "OTDR";
					default:	// All other opcodes are NOPD
						return "NOP";
				}
			}
		}


	
		/// <summary>
		/// Disassembly of CB xx codes
		/// </summary>
		/// <param name="opcode">opcode to execute</param>
		private string Disassemble_CB(byte opcode)
		{

			// Operations with single byte register
			// The format is 00 ooo rrr where ooo is the operation and rrr is the register
			string reg = GetHalfRegister(opcode);

			return Disassemble_CB_on_reg(opcode, reg);

		}


		/// <summary>
		/// Disassembly of DD CB xx codes or FD CB xx codes
		/// </summary>
		/// <param name="Address">Address to act on - Address = I_ + d</param>
		/// <param name="opcode">opcode</param>
		private string Disassemble_DDFD_CB(string Address, byte opcode)
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


			string targetReg;

			// Check if the operation is a bit checking operation
			// The format is 01 bbb rrr
			if (opcode >> 6 == 0x01)
			{
				targetReg = "";
			}
			else
			{
				// Retrieve the register from opcode xxxxx rrr
				targetReg = GetHalfRegister(opcode);

				// Check if the source is (I_ + d) so the op will not act on any register
				// but only on memory
				if (targetReg.StartsWith("("))
					targetReg = "";
				else
					targetReg = string.Format("(undocumented) LD {0},", targetReg);;
			}

			return targetReg + Disassemble_CB_on_reg(opcode, Address);

		}


		/// <summary>
		/// This is the low level function called within a CB opcode fetch
		/// (single byte or DD CB or FD CB)
		/// It must be called after the execution unit has determined on
		/// wich register act
		/// </summary>
		/// <param name="opcode">opcode</param>
		/// <param name="reg">Register to act on</param>
		private string Disassemble_CB_on_reg(byte opcode, string reg)
		{
			switch (opcode >> 3)
			{
				case 0:	// RLC r
					return string.Format("RLC {0}", reg);
				case 1:	// RRC r
					return string.Format("RRC {0}", reg);
				case 2: // RL r
					return string.Format("RL {0}", reg);
				case 3: // RR r
					return string.Format("RR {0}", reg);
				case 4: // SLA r
					return string.Format("SLA {0}", reg);
				case 5: // SRA r
					return string.Format("SRA {0}", reg);
				case 6: // SLL r
					return string.Format("SLL {0}", reg);
				case 7: // SRL r
					return string.Format("SRL {0}", reg);
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
							return string.Format("BIT {0},{1}",bit, reg);
						case 2: // RES n,r
							return string.Format("RES {0},{1}",bit, reg);
						case 3: // SET n,r
							return string.Format("SET {0},{1}",bit, reg);
						default:
							throw new Exception("What am I doing here?!?");
					}
			}
		}

		#endregion

	}
}
