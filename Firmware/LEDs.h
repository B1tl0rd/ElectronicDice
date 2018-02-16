// LEDs.h

#ifndef _LEDS_h
#define _LEDS_h

#include "Arduino.h"
#include "MessageQueue.h"

//#define RGB_LED

#if defined(RGB_LED)
#include "APA102LEDs.h"
#else
#include "GPIOLEDs.h"
#include "GPIOLEDController.h"

#define GPIO_MsgType_LEDOn 5
#define GPIO_MsgType_LEDsOff 6
#endif

/// <summary>
/// Adapter class that uses either RGB leds or GPIO leds (for version 1 of the dice)
/// </summary>
class LEDs
{
public:
	LEDs();
	void init();
	void update();
	void stop();

	void setLEDNow(int face, int led, uint32_t color); // Index 0 - 20
	void setLEDNow(int index, uint32_t color); // Index 0 - 20
	void setLEDsNow(int indices[], uint32_t colors[], int count);
	void setAllNow(uint32_t color);
	void clearAllNow();

	void setLED(int face, int led, uint32_t color); // Index 0 - 20
	void setLED(int index, uint32_t color); // Index 0 - 20
	void setLEDs(int indices[], uint32_t colors[], int count);
	void setAll(uint32_t color);
	void clearAll();

	static int ledIndex(int face, int led);

#if defined(RGB_LED)
	Devices::APA102LEDs RGBLeds;
#else
	Devices::GPIOLEDs GPIOLeds;
	GPIOLEDController controller;
#endif
	Core::MessageQueue messageQueue;

	int queuedIndices[21];
	uint32_t queuedColors[21];
};

extern LEDs leds; 

#endif
