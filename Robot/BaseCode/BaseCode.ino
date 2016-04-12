#include "DualVNH5019MotorShield.h"
#include <PID_v1.h>

DualVNH5019MotorShield md;

#define encodPinA1      18                       // encoder A pin
#define encodPinB1      16                       // encoder B pin
#define encodPinA2      17                       // encoder A pin
#define encodPinB2      15                       // encoder B pin

#define CURRENT_LIMIT   1000                     // high current warning
#define LOW_BAT         10000                   // low bat warning
#define LOOPTIME        100                     // PID loop time

unsigned long lastMilli = 0;                    // loop timing 
unsigned long lastMilliPrint = 0;               // loop timing
double speed_req_r = 0;                            // speed (Set Point)
double speed_act_r = 0;                              // speed (actual value)
double speed_req_l = 0;                            // speed (Set Point)
double speed_act_l = 0;                              // speed (actual value)
double PWM_val_r = 0;                                // (25% = 64; 50% = 127; 75% = 191; 100% = 255)
double PWM_val_l = 0;
int voltage = 0;                                // in mV
int current = 0;                                // in mA
volatile long countr = 0;                       // rev counter
volatile long countl = 0;                       // rev counter

//Define the aggressive and conservative Tuning Parameters
double aggKp=4, aggKi=0.2, aggKd=1;
double consKp=2.7, consKi=0.25, consKd=0.005;

//Specify the links and initial tuning parameters
PID RPID(&speed_act_r, &PWM_val_r, &speed_req_r, consKp, consKi, consKd, DIRECT);
PID LPID(&speed_act_l, &PWM_val_l, &speed_req_l, consKp, consKi, consKd, DIRECT);

String inputString = "";         // a string to hold incoming data
boolean stringComplete = false;  // whether the string is complete
char message[24] = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
char messB[12] = {0,0,0,0,0,0,0,0,0,0,0};
char packet[11] = {0,0,0,0,0,0,0,0,0,0};
double scale = 0.0;

void setup() {
  delay(1000);

  analogReference(EXTERNAL);                            // Current external ref is 3.3V
  pinMode(encodPinA1, INPUT); 
  pinMode(encodPinB1, INPUT); 
  digitalWrite(encodPinA1, HIGH);                      // turn on pullup resistor
  digitalWrite(encodPinB1, HIGH);
  pinMode(encodPinA2, INPUT); 
  pinMode(encodPinB2, INPUT); 
  digitalWrite(encodPinA2, HIGH);                      // turn on pullup resistor
  digitalWrite(encodPinB2, HIGH);
  attachInterrupt(digitalPinToInterrupt(18), rencoder, FALLING);
  attachInterrupt(digitalPinToInterrupt(3), lencoder, RISING);
  
  // initialize serial:
  Serial.begin(115200);
  // reserve 200 bytes for the inputString:
  md.init();
  md.setBrakes(0,0);
  inputString.reserve(200);

  //turn the PID on
  RPID.SetMode(AUTOMATIC);
  LPID.SetMode(AUTOMATIC);
}

void loop() {
  serialEvent(); //call the function
  // print the string when a newline arrives:

  if((millis()-lastMilli) >= LOOPTIME)   {                                    // enter tmed loop
   lastMilli = millis();
   getMotorDataR();
   getMotorDataL();   
   
   if(speed_req_r < 0 || speed_req_l < 0) {
      md.setSpeeds((speed_req_r*0.75), speed_req_l);
   }
   else {
      RPID.Compute();
      LPID.Compute();
      if( speed_req_r == 0) {
          PWM_val_r = 0;    
      } 
      if( speed_req_l == 0) {
          PWM_val_l = 0;    
      }
        md.setSpeeds(PWM_val_r, PWM_val_l);
   }
 }
  
  if (stringComplete) {    
    //char[] messgae = new char[inputString.length];
    inputString.toCharArray(message, 24);
    //byte[] messB = new byte[message.Length >> 1];
    for (int i = 0; i < 12; ++i)
    {
        messB[i] = (byte)((GetHexValue(message[i << 1])) << 4 | (GetHexValue(message[(i << 1) + 1])));
    } 
    
    COBSdecode(messB, 12, packet);    
    //Serial.println(packet);
    if(packet[0] == 2) {
      //Echo the String
      Serial.println(inputString);
    } else if( packet[0] == 0 ) {
      byte sum = packet[0]+packet[1]+packet[2]+packet[3]+packet[4]+packet[5]+packet[6]+packet[7];
      //Checksum
      if(sum==(packet[8]&0xFF)) {
        
        //if(packet[1]&0x01) {
          //launch
        //}        
        int steering = packet[2] & 0xFF;
        int throttle = packet[1] & 0xFF;
        steering-=127;
        throttle-=127;
        switch (packet[7]) {
          case 0x1:
            steering/=4;
            throttle/=4;
            break;
          case 0x2:
            steering/=2;
            throttle/=2;
            break;
          case 0x4:
            steering;
            throttle;
            break;
          case 0x8:
            steering*=1.125;
            throttle*=1.125;
            break;
          case 0x10:
            steering*=1.25;
            throttle*=1.25;
            break;
          case 0x20:
            steering*=1.4;
            throttle*=1.4;
            break;
          case 0x40:
            steering*=1.56;
            throttle*=1.56;
            break;
          default:
            steering = 0;
            throttle = 0;
            break;          
        }
        speed_req_r = throttle;
        speed_req_l = steering;        
      }
    }
    // clear the string:
    inputString = "";
    stringComplete = false;
  }
}

void getMotorDataR()  {                                                        // calculate speed, volts and Amps
  static long countAntR = 0;                                                   // last count
  speed_act_r = ((countr - countAntR)*(60*(1000/LOOPTIME)))/(16*30);          // 16 pulses X 29 gear ratio = 464 counts per output shaft rev
  countAntR = countr;
}

void getMotorDataL()  {                                                        // calculate speed, volts and Amps
  static long countAntL = 0;                                                   // last count
  speed_act_l = ((countl - countAntL)*(60*(1000/LOOPTIME)))/(16*30);          // 16 pulses X 29 gear ratio = 464 counts per output shaft rev
  countAntL = countl;
}

void rencoder()  {                                    // pulse and direction, direct port reading to save cycles
 if (PIND & 0b00000100)            countr--;                // if(digitalRead(encodPinB1)==HIGH)   count ++;
 else if(!(PIND & 0b00000100))     countr++;                // if (digitalRead(encodPinB1)==LOW)   count --;
}

void lencoder()  {                                    // pulse and direction, direct port reading to save cycles
 if (PINE & 0b00100000)           countl++;                // if(digitalRead(encodPinB1)==HIGH)   count ++;
 else if(!(PINE & 0b00100000))    countl--;                // if (digitalRead(encodPinB1)==LOW)   count --;
}

int GetHexValue(char hex)
{
    int val = (int)hex;
    return val - (val < 58 ? 48 : 55);
}
/*
  SerialEvent occurs whenever a new data comes in the
 hardware serial RX.  This routine is run between each
 time loop() runs, so using delay inside loop can delay
 response.  Multiple bytes of data may be available.
 */
void serialEvent() {
  while (Serial.available()) {
    // get the new byte:
    char inChar = (char)Serial.read();
    // add it to the inputString:
    inputString += inChar;
    // if the incoming character is a newline, set a flag
    // so the main loop can do something about it:
    if (inChar == '\n') {
      stringComplete = true;
    }
  }
}

/// \brief Encode a byte buffer with the COBS encoder.
    /// \param source The buffer to encode.
    /// \param size The size of the buffer to encode.
    /// \param destination The target buffer for the encoded bytes.
    /// \returns The number of bytes in the encoded buffer.
    /// \warning destination must have a minimum capacity of
    ///     (size + size / 254 + 1).
    uint8_t COBSencode(const char* source, uint8_t size, char* destination)
    {
        size_t read_index  = 0;
        size_t write_index = 1;
        size_t code_index  = 0;
        uint8_t code       = 1;

        while(read_index < size)
        {
            if(source[read_index] == 0)
            {
                destination[code_index] = code;
                code = 1;
                code_index = write_index++;
                read_index++;
            }
            else
            {
                destination[write_index++] = source[read_index++];
                code++;

                if(code == 0xFF)
                {
                    destination[code_index] = code;
                    code = 1;
                    code_index = write_index++;
                }
            }
        }

        destination[code_index] = code;

        return write_index;
    }

    /// \brief Decode a COBS-encoded buffer.
    /// \param source The COBS-encoded buffer to decode.
    /// \param size The size of the COBS-encoded buffer.
    /// \param destination The target buffer for the decoded bytes.
    /// \returns The number of bytes in the decoded buffer.
    /// \warning destination must have a minimum capacity of
    ///     size
    uint8_t COBSdecode(const char* source, uint8_t size, char* destination)
    {
        size_t read_index  = 0;
        size_t write_index = 0;
        uint8_t code;
        uint8_t i;

        while(read_index < size)
        {
            code = source[read_index];

            if(read_index + code > size && code != 1)
            {
                return 0;
            }

            read_index++;

            for(i = 1; i < code; i++)
            {
                destination[write_index++] = source[read_index++];
            }

            if(code != 0xFF && read_index != size)
            {
                destination[write_index++] = '\0';
            }
        }
        
        return write_index;
    }




