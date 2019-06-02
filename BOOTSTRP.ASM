; 42 BYTE BOOTSTRAP LOADER WITH INIT FOR 8250
; INTENDED FOR THE H89 BUT MAY ASLO WORK ON H8.

LP      EQU     0E0H
RX      EQU     LP
DVL     EQU     LP
DVH     EQU     LP+1
IER     EQU     LP+1
LCNTL   EQU     LP+3
MCNTL   EQU     LP+4
LSTAT   EQU     LP+5

DBEND   EQU     265BH  ; FROM H89LDR.PRN

        ORG 2300H
LDR:    XRA A
        OUT LCNTL       ; LINE CONTROL
        OUT IER         ; NO INTERRUPTS
        OUT MCNTL       ; INIT MODEM CONTROL
        DCR A           ; SHOULD BE 'MVI A, 80H' BUT 0FFH OK
        OUT LCNTL
        MVI A, 0CH      ; 9600 BAUD
        OUT DVL
        XRA A
        OUT DVH
        MVI A, 07H      ; 8 BIT 2 STOPS
        OUT LCNTL       ; THIS WILL BE 2313, MUST NOT BE 0FAH
        IN LSTAT
        IN RX           ; CLEAR ANY JUNK
        LXI H, LDR1
        LXI D, DBEND-1  ; DEPENDS ON SIZE (TO BE DETERMINED)
LDR1:   IN LSTAT
        RAR
        JNC LDR1        ; WAIT FOR CHAR
        IN RX
        STAX D
        DCX D
        PCHL            ; TO BE OVER WRITTEN BY LOADER WITH NOP
                        ;  $ SHOULD BE 2329H


