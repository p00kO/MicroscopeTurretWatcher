int a, b,c,d, delta =200;
int p1 = 2;
int p2 = 3; 
int p3 = 5;
void setup() {
  // put your setup code here, to run once:
//pinMode(6, OUTPUT);
pinMode(p1, INPUT);
pinMode(p2, INPUT);
pinMode(p3, INPUT);
Serial.begin(9600);
}

void loop() {
  a=digitalRead(p1);
  b=digitalRead(p2);
  c=digitalRead(p3);


  if( (a==1) && (b==1) && (c==1) ) Serial.println("0");
  if( (a==1) && (b==1) && (c==0) ) Serial.println("1");
  if( (a==1) && (b==0) && (c==1) ) Serial.println("2");
  if( (a==1) && (b==0) && (c==0) ) Serial.println("3");
  if( (a==0) && (b==1) && (c==1) ) Serial.println("4");
  if( (a==0) && (b==1) && (c==0) ) Serial.println("5");
  if( (a==0) && (b==0) && (c==1) ) Serial.println("6");
  if( (a==0) && (b==0) && (c==0) ) Serial.println("7");
  
  delay(10);
}
