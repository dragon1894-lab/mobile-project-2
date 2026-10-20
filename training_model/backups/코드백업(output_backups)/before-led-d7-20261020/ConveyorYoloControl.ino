// PUL/DIR driver: matches the user's verified working sketch: PUL 5, DIR 6, 80 us.
// USB serial 9600 baud. Starts stopped; RUN must be requested explicitly.
#include <string.h>
const int PUL_PIN = 5;
const int DIR_PIN = 6;
const unsigned int PULSE_DELAY = 80; // nominal microseconds per half pulse
const unsigned long PC_TIMEOUT_MS = 3000;

bool conveyorRunning = false;
unsigned long lastPcMessage = 0;
char commandBuffer[32];
byte commandLength = 0;
bool commandOverflow = false;

void conveyorForward() {
  digitalWrite(DIR_PIN, LOW);
  conveyorRunning = true;
}

void conveyorStop() {
  conveyorRunning = false;
  digitalWrite(PUL_PIN, HIGH); // same idle level as the original sketch
}

void printState() {
  Serial.println(conveyorRunning ? "RUNNING" : "STOPPED");
}

void handleCommand() {
  commandBuffer[commandLength] = '\0';
  if (strcmp(commandBuffer, "STOP") == 0) {
    conveyorStop();
    lastPcMessage = millis();
    printState();
  } else if (strcmp(commandBuffer, "RUN") == 0) {
    lastPcMessage = millis();
    conveyorForward();
    printState();
  } else if (strcmp(commandBuffer, "PING") == 0) {
    lastPcMessage = millis();
    printState();
  } else if (strcmp(commandBuffer, "HELLO") == 0) {
    lastPcMessage = millis();
    Serial.print("PL_CONVEYOR_PUL_DIR_V3 ");
    printState();
  } else {
    Serial.println("ERROR UNKNOWN_COMMAND");
  }
}

void setup() {
  pinMode(PUL_PIN, OUTPUT);
  pinMode(DIR_PIN, OUTPUT);
  digitalWrite(DIR_PIN, LOW);
  conveyorStop();
  Serial.begin(9600);
  lastPcMessage = millis();
  Serial.println("PL_CONVEYOR_PUL_DIR_V3 STOPPED");
}

void loop() {
  // Bounded serial work prevents a byte stream starving pulses/watchdog.
  for (byte count = 0; count < 16 && Serial.available() > 0; ++count) {
    char value = (char)Serial.read();
    if (value == '\r') continue;
    if (value == '\n') {
      if (commandOverflow) Serial.println("ERROR COMMAND_TOO_LONG");
      else if (commandLength > 0) handleCommand();
      commandLength = 0;
      commandOverflow = false;
    } else if (commandLength < sizeof(commandBuffer) - 1) {
      commandBuffer[commandLength++] = value;
    } else {
      commandOverflow = true;
    }
  }
  if (conveyorRunning && (unsigned long)(millis() - lastPcMessage) >= PC_TIMEOUT_MS) {
    conveyorStop();
    Serial.println("WATCHDOG_STOP");
  }
  if (conveyorRunning) {
    // One pulse per loop; no blocking 5-second run or 3-second stop.
    digitalWrite(PUL_PIN, LOW);
    delayMicroseconds(PULSE_DELAY);
    digitalWrite(PUL_PIN, HIGH);
    delayMicroseconds(PULSE_DELAY);
  }
}
