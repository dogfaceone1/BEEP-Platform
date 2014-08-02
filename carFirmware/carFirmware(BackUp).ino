#include <Servo.h>

#include <SoftPWM.h>
#include <SoftPWM_timer.h>

//#include <PWMServo.h>

const int HeadLeft = 8;
const int HeadRight = 7;
const int TailLeft1 = 15;
const int TailLeft2 = 14;
const int TailLeft3 = 12;
const int TailRight1 = 9;
const int TailRight2 = 10;
const int TailRight3 = 11;
const int Sonar1T = 21;
const int Sonar1E = 20;
const int Sonar2T = 19;
const int Sonar2E = 18;
const int Sonar3T = 17;
const int Sonar3E = 16;

Servo steering;
Servo throttle;
Servo tilt;
const int sMax = 99;
const int sNeutral = 58;
const int sMin = 27;
const int tMax = 180;
const int tNeutral = 90;
const int tMin = 45;
const int tiMin = 130;
const int tiMax = 170;
const int TTLreset = 150;
int steeringAngle = 115;
int throttlePos =115;
int TTL = 0;
int tiltAngle = 170;
boolean lightOn = false;

void setup()
{
  //analogWriteFrequency(3, 50);
  //analogWriteFrequency(5, 50);  
  steering.attach(3);
  throttle.attach(4);
  tilt.attach(5);
  pinMode(2,OUTPUT);
 // Serial.begin(9600);
  Serial1.begin(9600);
 // Serial.println("I'm Alive");
  //SendMessage("I'm Alive");
  pinMode(13,OUTPUT);
  digitalWrite(2,LOW);
  setUpLEDS();
  setUpSonars();
  FlashAll(1,500);
}
void setUpSonars()
{
  pinMode(Sonar1T,OUTPUT);
  pinMode(Sonar1E,INPUT); 
  digitalWrite(Sonar1T,HIGH);
  pinMode(Sonar2T,OUTPUT);
  pinMode(Sonar2E,INPUT); 
  digitalWrite(Sonar2T,LOW);
  pinMode(Sonar3T,OUTPUT);
  pinMode(Sonar3E,INPUT); 
  digitalWrite(Sonar3T,LOW);
}
void setUpLEDS()
{
  pinMode(HeadLeft, OUTPUT);
  pinMode(HeadRight, OUTPUT);
  SoftPWMBegin();
  SoftPWMSet(TailLeft1,0);
  SoftPWMSet(TailLeft2,0);
  SoftPWMSet(TailLeft3,0);
  SoftPWMSet(TailRight1,0);
  SoftPWMSet(TailRight2,0);
  SoftPWMSet(TailRight3,0);
  SoftPWMSetFadeTime(TailLeft1,250,250);
  SoftPWMSetFadeTime(TailLeft2,250,250);
  SoftPWMSetFadeTime(TailLeft3,250,250);
  SoftPWMSetFadeTime(TailRight1,250,250);
  SoftPWMSetFadeTime(TailRight2,250,250);
  SoftPWMSetFadeTime(TailRight3,250,250);
}
void calibrate()
{
  digitalWrite(13,HIGH);
  SetLEDColor(0,200,100,0);
  SetLEDColor(1,200,100,0);
  digitalWrite(2,LOW);
  delay(500);
  
  steeringAngle = sNeutral;
  steering.write(steeringAngle);
  throttlePos = tNeutral;
  throttle.write(tNeutral);
  digitalWrite(2,HIGH);
  delay(1500);
  throttle.write(tNeutral +10);
  delay(500);
  throttle.write(tNeutral);
  digitalWrite(13,LOW);
  SetLEDColor(0,0,0,0);
  SetLEDColor(1,0,0,0);
}
void loop(){
    CheckForCommands();
    PostServos();
    //Serial.println("Loop");
    delay(10);
}
void PostServos()
{
  if(TTL > 0)
  {
     steering.write(steeringAngle);
     throttle.write(throttlePos);
     tilt.write(tiltAngle);
     TTL -= 1;
  }
  else
  {
    TTL = -1;
    steeringAngle = sNeutral;
    throttlePos = tNeutral;
    tiltAngle = 170;
    steering.write(steeringAngle);
    throttle.write(throttlePos);
    tilt.write(tiltAngle);
    digitalWrite(2,LOW);
    FlashColor(1,250,50,0,0);
    
  }

}
void SendMessage(String mess)
{
  int length = mess.length();
  Serial1.print(length,BYTE);
  Serial1.print(mess);
  //Serial.println("Sent: "+ mess);
}
void CheckForCommands()
{
  if(Serial1.available() > 0)
  {

      byte length = Serial1.read();
      char data[50];// = new char[length];
      if(length > 50)
      {
        delay(10);
        Serial.flush();
        return;
      }
      int timeout = 100;
      while(Serial1.available() < length && timeout > 0)
      {
         delay(1); 
         timeout--;
      }
      if(timeout <= 0)
      {
        return; 
      }
      for(byte i = 0; i< length;i++)
      {
        data[i] = Serial1.read();
      }
      String dataString = String(data);
      dataString = dataString.substring(0,length);
//      Serial.print("Recieve: ");
//      Serial.print(dataString);
//      Serial.print(":");
//      Serial.print(length);
//      Serial.print(":");
//      Serial.println(dataString.length());
      if(dataString.startsWith("Lili Says Hi"))
      {
         calibrate();
         TTL = TTLreset;
         FlashAll(2,500);
         SendMessage("Hello Lili"); 
      }
      else if(dataString.startsWith("th"))
      {
          throttlePos = dataString.substring(2).toInt();
          throttlePos = ((throttlePos * (tMax - tMin)) /100) + tMin;
          //Serial.print("Throttle set to ");
          //Serial.println(throttlePos);
      }
      else if(dataString.startsWith("st"))
      {
          steeringAngle = dataString.substring(2).toInt();
          steeringAngle = ((steeringAngle * (sMax - sMin)) /100) + sMin;
          //Serial.print("Steering set to ");
          //Serial.println(steeringAngle);
      }
      else if(dataString.startsWith("ti"))
      {
          tiltAngle = dataString.substring(2).toInt();
          tiltAngle = ((tiltAngle * (tiMax - tiMin)) /100) + tiMin;
          //Serial.print("Steering set to ");
          //Serial.println(steeringAngle);
      }
      else if(dataString.startsWith("SONAR"))
      {
         SendMessage("BOP");
         SendMessage("SONAR" + ReadSonar(dataString.substring(5).toInt())); 
      }
      else if(dataString.startsWith("EE"))
      {
        TTL = 0;
        digitalWrite(2, LOW);
      }
      else if(dataString.startsWith("Beat"))
      {
          TTL = TTLreset;
          SendMessage("BOP");
          if(lightOn)
          {
            lightOn = false;
            digitalWrite(13,LOW);
          }
          else
          {
            lightOn = true;
            digitalWrite(13,HIGH);
          }
          
          //Serial.println("Heart Beat Recived");
      }
      else if(dataString.startsWith("LED"))
      {
         if( dataString.startsWith("LEDlf"))
         {
            int val = dataString.substring(5).toInt();
            digitalWrite(HeadLeft,val);
         }
         else if( dataString.startsWith("LEDrf"))
         {
           int val = dataString.substring(5).toInt();
            digitalWrite(HeadRight,val);
         }
         else if( dataString.startsWith("LEDlr"))
         {
            int r =  dataString.substring(5,7).toInt();
            int g =  dataString.substring(8,10).toInt();
            int b =  dataString.substring(11,13).toInt();
            SetLEDColor(0,r,g,b);
            
         }
         else if( dataString.startsWith("LEDrr"))
         {
           int r =  dataString.substring(5,7).toInt();
            int g =  dataString.substring(8,10).toInt();
            int b =  dataString.substring(11,13).toInt();
            SetLEDColor(1,r,g,b);
         }
      }
      
  }
}
void FlashAll(int count,int del)
{
  for(int i =0; i < count;i++)
  {
         digitalWrite(HeadRight,HIGH);
         digitalWrite(HeadLeft,HIGH);
         SetLEDColor(0,255,255,255);
         SetLEDColor(1,255,255,255); 
         delay(del);
         digitalWrite(HeadRight,LOW);
         digitalWrite(HeadLeft,LOW);
         SetLEDColor(0,0,0,0);
         SetLEDColor(1,0,0,0); 
         delay(del);
  }
}
void FlashColor(int count,int del,int r,int g,int b)
{
  for(int i =0; i < count;i++)
  {
         digitalWrite(13,HIGH);
         SetLEDColor(0,r,g,b);
         SetLEDColor(1,r,g,b); 
         delay(del);
         digitalWrite(13,LOW);
         SetLEDColor(0,0,0,0);
         SetLEDColor(1,0,0,0); 
         delay(del);
  }
}
void SetLEDColor(int sel,int R,int G,int B)
{
  if(sel == 0)
  {
    SoftPWMSet(TailLeft1,B);
    SoftPWMSet(TailLeft2,G);
    SoftPWMSet(TailLeft3,R);
  }
  else
  {
    SoftPWMSet(TailRight1,R);
    SoftPWMSet(TailRight2,G);
    SoftPWMSet(TailRight3,B);  
  }
    
}
String ReadSonar(int index)
{  
  String output;
  int sel = Sonar1T;
  switch(index)
  {
   case 0:
    sel = Sonar1T;
    break;
   case 1:
   sel = Sonar2T;
    break;
   case 2:
   sel = Sonar3T;
    break;
   default:
   sel = Sonar1T;
    break; 
  }
    int count=0;
    int timeout = 200000;
    digitalWrite(21,HIGH);
    if(digitalRead(20) == HIGH)
    {
      while(digitalRead(20) == HIGH && timeout > 0)
      {
         delayMicroseconds(1); 
         timeout--;
      }
    }
    while(digitalRead(20) == LOW && timeout > 0)
    {
       delayMicroseconds(1); 
       timeout--;
    }
    while(digitalRead(20) == HIGH && timeout > 0)
    {
      delayMicroseconds(1);
      count++; 
    }
    //digitalWrite(sel,LOW);
    float dis = count / 147.0;
    //count = pulseIn(SonarE,HIGH,20);
    dis = 2.54 * dis;
    output = String(dis);
  
    return output;
}
