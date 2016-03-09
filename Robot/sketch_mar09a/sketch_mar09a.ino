#include "DualVNH5019MotorShield.h"

DualVNH5019MotorShield md;

// Test MD03a / Pololu motor with encoder
// speed control (PI), V & I display
// Credits:
//   Dallaby   http://letsmakerobots.com/node/19558#comment-49685
//   Bill Porter  http://www.billporter.info/?p=286
//   bobbyorr (nice connection diagram) http://forum.pololu.com/viewtopic.php?f=15&t=1923
//Interrupt Pin: 2, 3, 18, 19, 20, 21
#define encodPinA1      18                       // encoder A pin
#define encodPinB1      16                       // encoder B pin
#define encodPinA2      17                       // encoder A pin
#define encodPinB2      15                       // encoder B pin

#define CURRENT_LIMIT   1000                     // high current warning
#define LOW_BAT         10000                   // low bat warning
#define LOOPTIME        100                     // PID loop time

unsigned long lastMilli = 0;                    // loop timing 
unsigned long lastMilliPrint = 0;               // loop timing
int speed_req_r = 100;                            // speed (Set Point)
int speed_act_r = 0;                              // speed (actual value)
int speed_req_l = 100;                            // speed (Set Point)
int speed_act_l = 0;                              // speed (actual value)
int PWM_val_r = 0;                                // (25% = 64; 50% = 127; 75% = 191; 100% = 255)
int PWM_val_l = 0;
int voltage = 0;                                // in mV
int current = 0;                                // in mA
volatile long countr = 0;                       // rev counter
volatile long countl = 0;                       // rev counter
float Kp =   2;                                // PID proportional control Gain
float Kd =   .5;                                // PID Derivitave control gain


void setup() {
 analogReference(EXTERNAL);                            // Current external ref is 3.3V
 Serial.begin(115600);
 pinMode(encodPinA1, INPUT); 
 pinMode(encodPinB1, INPUT); 
 digitalWrite(encodPinA1, HIGH);                      // turn on pullup resistor
 digitalWrite(encodPinB1, HIGH);
 pinMode(encodPinA2, INPUT); 
 pinMode(encodPinB2, INPUT); 
 digitalWrite(encodPinA2, HIGH);                      // turn on pullup resistor
 digitalWrite(encodPinB2, HIGH);
 attachInterrupt(digitalPinToInterrupt(18), rencoder, FALLING);
 attachInterrupt(digitalPinToInterrupt(19), lencoder, FALLING);
 md.init();

}
void loop() {
 getParam();                                                                 // check keyboard
 if((millis()-lastMilli) >= LOOPTIME)   {                                    // enter tmed loop
   lastMilli = millis();
   getMotorDataR();
   getMotorDataL();
   PWM_val_r = updatePid(PWM_val_r, speed_req_r, speed_act_r);
   PWM_val_l = updatePid(PWM_val_l, speed_req_l, speed_act_l);
   md.setSpeeds(PWM_val_r, 0);
 }
 printMotorInfo();                                                           // display data
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

int updatePid(int command, int targetValue, int currentValue)   {             // compute PWM value
float pidTerm = 0;                                                            // PID correction
int error=0;                                  
static int last_error=0;                             
 error = abs(targetValue) - abs(currentValue); 
 pidTerm = (Kp * error) + (Kd * (error - last_error));                            
 last_error = error;
 return constrain(command + int(pidTerm), 0, 255);
}

void printMotorInfo()  {                                                      // display data
 if((millis()-lastMilliPrint) >= 500)   {                     
   lastMilliPrint = millis();
   Serial.print("R\tSP:");             Serial.print(speed_req_r);  
   Serial.print("  RPM:");          Serial.print(speed_act_r);
   Serial.print("  PWM:");          Serial.print(PWM_val_r); 
   Serial.print("L\tSP:");             Serial.print(speed_req_l);  
   Serial.print("  RPM:");          Serial.print(speed_act_l);
   Serial.print("  PWM:");          Serial.print(PWM_val_l);
   Serial.println(); 
  }
}

void rencoder()  {                                    // pulse and direction, direct port reading to save cycles
 if (PIND & 0b00000100)    countr--;                // if(digitalRead(encodPinB1)==HIGH)   count ++;
 else                      countr++;                // if (digitalRead(encodPinB1)==LOW)   count --;
}

void lencoder()  {                                    // pulse and direction, direct port reading to save cycles
 if (PIND & 0b00001000)    countl--;                // if(digitalRead(encodPinB1)==HIGH)   count ++;
 else                      countl++;                // if (digitalRead(encodPinB1)==LOW)   count --;
}

int getParam()  {
char param, cmd;
 if(!Serial.available())    return 0;
 delay(10);                  
 param = Serial.read();                              // get parameter byte
 if(!Serial.available())    return 0;
 cmd = Serial.read();                                // get command byte
 Serial.flush();
 switch (param) {
   case 'v':                                         // adjust speed
     if(cmd=='+')  {
       speed_req_r += 20;
       if(speed_req_r>400)   speed_req_r=400;
     }
     if(cmd=='-')    {
       speed_req_r -= 20;
       if(speed_req_r<0)   speed_req_r=0;
     }
     break;
   case 's':                                        // adjust direction
     if(cmd=='+'){
       //digitalWrite(InA1, LOW);
       //digitalWrite(InB1, HIGH);
     }
     if(cmd=='-')   {
       //digitalWrite(InA1, HIGH);
       //digitalWrite(InB1, LOW);
     }
     break;
   case 'o':                                        // user should type "oo"
     //digitalWrite(InA1, LOW);
     //digitalWrite(InB1, LOW);
     speed_req_r = 0;
     break;
   default: 
     Serial.println("???");
   }
}
