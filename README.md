BBEP-Platform
=============

Windows Phone 8 remote control vehical platform designed to provide a low cost rootic platform for reaserchers or hobbiest.

This project was created for Proffesor Lin's 499 AI class at Cal Poly Pomona in Pomona Califonia.

The project is made up of three orginal components.
1. The vehical hardware
2. The vehical firmware
3. THe windows phone software

The Hardware:
The vehical hardware is based around a Teensy 3.1 microcontroller runnign the Car Frimware areduino code. The microcotroller is used to control a set of hardware on a custom PCB that allows it to control the phiysical vehical. This will be upladed later.

The Firmware:
The Car Firmware is and arduino program written for a Teensy 3.1 microcotroller and is designed to interface with the main phone software through a bluethooth serial connection.

The Windows Phone Software:
The main software for the plat form consits of several classes that provide an abstraction layer to the car firmware that is controlled via bluetooth. More details later.