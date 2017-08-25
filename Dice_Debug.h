// Dice_Debug.h

#ifndef _DICE_DEBUG_h
#define _DICE_DEBUG_h

#include "Arduino.h"

struct DiceDebug
{
private:
	bool displayDebug;

public:
	DiceDebug();
	void begin();
	void stop();
	bool isDebugOn();

	void print(const char string[]);
	void print(char);
	void print(unsigned char, int = DEC);
	void print(int, int = DEC);
	void print(unsigned int, int = DEC);
	void print(long, int = DEC);
	void print(unsigned long, int = DEC);
	void print(double, int = 2);
	void print(const Printable&);
	
	void println(const __FlashStringHelper *);
	void println(const String &s);
	void println(const char[]);
	void println(char);
	void println(unsigned char, int = DEC);
	void println(int, int = DEC);
	void println(unsigned int, int = DEC);
	void println(long, int = DEC);
	void println(unsigned long, int = DEC);
	void println(double, int = 2);
	void println(const Printable&);
	void println(void);
};

extern DiceDebug diceDebug;

#endif

