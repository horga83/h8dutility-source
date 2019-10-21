DPB17S:	DW	20		; Sectors per Track
	DB	3			; Block Shift = Allocation Block (AB)
	Size 1024
	DB	7			; Block Mask
	DB	0			; Extent Mask
	DW	91			; Max ALB #
	DW	63			; # Directory entries - 1
	DB	192			; Bit map for ALB 11000000b each one indicates an AB used for the Dir
	DB	0			; used for directory
	DW	16			;# bytes in dir. check buffer
	DW	3			;# tracks before directory