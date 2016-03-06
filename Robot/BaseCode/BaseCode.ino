#include "DualVNH5019MotorShield.h"

DualVNH5019MotorShield md;

String inputString = "";         // a string to hold incoming data
boolean stringComplete = false;  // whether the string is complete
char message[24] = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
char messB[12] = {0,0,0,0,0,0,0,0,0,0,0};
char packet[11] = {0,0,0,0,0,0,0,0,0,0};
double scale = 0.0;

void setup() {
  delay(1000);
  // initialize serial:
  Serial.begin(9600);
  // reserve 200 bytes for the inputString:
  md.init();
  md.setBrakes(0,0);
  inputString.reserve(200);
}

void loop() {
  serialEvent(); //call the function
  // print the string when a newline arrives:
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
        
        if(packet([1]&0x01) {
          //launch
        }        
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
        md.setM1Speed((throttle*.75));
        md.setM2Speed(steering);

        switch (packet[6]) {
          case 0x1:
            md.setBrakes(0,0);
            break;
          case 0x2:
            md.setBrakes(50,50);
            break;
          case 0x4:
            md.setBrakes(100,1000);
            break;
          case 0x8:
            md.setBrakes(200,200);
            break;
          case 0x10:
            md.setBrakes(250,250);
            break;
          case 0x20:
            md.setBrakes(300,300);
            break;
          case 0x40:
            md.setBrakes(400,0);
            break;
          default:
            md.setBrakes(0,0);
            break;          
        }
        
      }
    }
    // clear the string:
    inputString = "";
    stringComplete = false;
  }
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




