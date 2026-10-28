"""Render the Edge Time product demo with Pillow and FFmpeg.
Requires Python, Pillow and FFmpeg on PATH. Output is a designed demonstration,
not a recording of a live desktop session.
"""
import argparse, math, shutil, subprocess
from datetime import date
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
W,H,FPS,DURATION=1280,720,24,16
ROOT=Path(__file__).resolve().parent
FONT_CN='C:/Windows/Fonts/msyh.ttc';FONT_BOLD='C:/Windows/Fonts/msyhbd.ttc';FONT_EN='C:/Windows/Fonts/segoeuib.ttf'
def f(path,size): return ImageFont.truetype(path,size)
F={n:f(path,size) for n,path,size in [('brand',FONT_EN,29),('head',FONT_BOLD,53),('sub',FONT_CN,24),('small',FONT_CN,17),('clock',FONT_EN,42),('date',FONT_EN,18),('day',FONT_CN,16),('button',FONT_CN,17),('mini',FONT_EN,16)]}
WHITE=(239,249,252,255);PALE=(183,210,223,255)
def rgba_bg():
 im=Image.new('RGB',(W,H));pix=im.load()
 for y in range(H):
  for x in range(W):
   q=max(0,1-math.hypot((x-1020)/1000,(y-270)/800));pix[x,y]=(int(16+16*q),int(25+28*q),int(40+40*q))
 layer=Image.new('RGBA',(W,H));d=ImageDraw.Draw(layer);d.ellipse((460,10,1310,840),fill=(100,176,205,35));return Image.alpha_composite(im.convert('RGBA'),layer.filter(ImageFilter.GaussianBlur(130)))
BG=rgba_bg()
def panel(kind, running=False, countdown='25:00'):
 im=Image.new('RGBA',(360,400));d=ImageDraw.Draw(im)
 d.rounded_rectangle((0,0,359,399),radius=25,fill=(53,91,113,225),outline=(187,223,236,35),width=1)
 if kind=='calendar':
  d.text((23,19),'12:00',font=F['clock'],fill=WHITE);d.text((23,82),'OCT 2026',font=F['date'],fill=WHITE)
  d.rounded_rectangle((21,113,339,145),radius=8,fill=(157,203,221,42))
  for i,t in enumerate('一二三四五六日'): d.text((42+i*44,120),t,font=F['day'],fill=PALE,anchor='ma')
  dates=[28,29,30,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,1,2,3,4,5,6,7,8]
  for i,v in enumerate(dates):
   x=51+(i%7)*44;y=168+(i//7)*34
   if i==30:d.rounded_rectangle((x-16,y-8,x+16,y+22),radius=8,fill=(192,229,242,255))
   color=(30,69,89,255) if i==30 else ((237,248,252,255) if 3<=i<=33 else (147,180,196,255))
   d.text((x,y-5),str(v),font=F['day'],fill=color,anchor='ma')
 else:
  if not running:d.text((23,20),'番茄钟',font=F['button'],fill=WHITE)
  d.text((180,85),countdown,font=F['clock'],fill=WHITE,anchor='ma')
  if not running:
   d.line((27,112,41,112),fill=WHITE,width=2);d.line((317,112,331,112),fill=WHITE,width=2);d.line((324,105,324,119),fill=WHITE,width=2)
   d.rounded_rectangle((22,178,338,226),radius=10,fill=(186,222,235,44));d.text((34,190),'连续轮换',font=F['button'],fill=WHITE)
   d.rounded_rectangle((287,187,326,214),radius=13,fill=(156,198,214,95));d.ellipse((292,190,313,211),fill=WHITE)
  d.rounded_rectangle((22,342,282,380),radius=9,fill=(210,234,242,53));d.text((152,350),'暂停' if running else '开始',font=F['button'],fill=WHITE,anchor='ma')
  d.rounded_rectangle((291,342,338,380),radius=9,fill=(210,234,242,53));d.arc((304,350,324,370),35,325,fill=WHITE,width=2);d.line((322,349,323,355,317,355),fill=WHITE,width=2)
 # custom two-row vector switch
 d.line((306,29,331,29),fill=WHITE,width=3);d.line((326,24,331,29,326,34),fill=WHITE,width=3);d.line((334,42,309,42),fill=WHITE,width=3);d.line((314,37,309,42,314,47),fill=WHITE,width=3)
 return im
CAL=panel('calendar');TIMER=panel('timer');ACTIVE1=panel('timer',True,'24:59');ACTIVE2=panel('timer',True,'24:58')
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def paste_alpha(bg,overlay,x,y,opacity=1,scale=1):
 im=overlay
 if scale!=1:im=im.resize((max(1,round(im.width*scale)),max(1,round(im.height*scale))),Image.Resampling.LANCZOS)
 if opacity<1:im=im.copy();im.putalpha(im.getchannel('A').point(lambda a:int(a*opacity)))
 bg.alpha_composite(im,(int(x),int(y)))
def caption(im,title,sub,alpha=1):
 layer=Image.new('RGBA',(W,H));d=ImageDraw.Draw(layer);d.text((92,106),'Edge Time',font=F['brand'],fill=PALE);d.text((88,212),title,font=F['head'],fill=WHITE);d.text((92,305),sub,font=F['sub'],fill=PALE);d.line((92,379,480,379),fill=(156,196,215,95),width=1);paste_alpha(im,layer,0,0,alpha)
def frame(t):
 im=BG.copy();d=ImageDraw.Draw(im)
 if t<4:
  caption(im,'时间，留在身边','贴边收起 · 点击展开',smooth(t/.6))
  if t<1.7:
   d.rounded_rectangle((1168,272,1212,408),radius=14,fill=(91,143,169,150));d.text((1190,295),'12',font=F['mini'],fill=WHITE,anchor='ma');d.text((1190,319),'00',font=F['mini'],fill=WHITE,anchor='ma');d.text((1190,348),'10/28',font=F['small'],fill=PALE,anchor='ma')
  else:
   q=smooth((t-1.7)/.55);size=.14+.86*q;x=1212-360*size;y=340-200*size;paste_alpha(im,CAL,x,y,1,size)
 elif t<8:
  caption(im,'自己的时区','联网校准 · 日历与时间',smooth((t-4)/.45));paste_alpha(im,CAL,750,154)
  d.text((92,424),'电脑时区不同，时间依然清晰。',font=F['small'],fill=PALE)
 elif t<12:
  caption(im,'专注，也记得休息','番茄钟 · 单次或连续轮换',smooth((t-8)/.45));paste_alpha(im,TIMER if t<9.45 else ACTIVE1 if t<10.7 else ACTIVE2,750,154)
  if 8.55<t<9.5:
   q=smooth((t-8.55)/.6);x=1055-140*q;y=610-90*q
   d.polygon([(x,y),(x,y+25),(x+7,y+18),(x+12,y+28),(x+17,y+26),(x+12,y+16),(x+22,y+16)],fill=WHITE,outline=(29,52,69,255))
  d.text((92,424),'时间到，自定义提醒。',font=F['small'],fill=PALE)
 elif t<14.7:
  caption(im,'留一点空间给自己','自定义提醒文字',smooth((t-12)/.4))
  d.rounded_rectangle((724,225,1135,452),radius=24,fill=(45,72,90,230));d.text((760,269),'Time to Exercise',font=F['brand'],fill=WHITE);d.rounded_rectangle((760,363,870,408),radius=9,fill=(206,231,240,60));d.text((785,372),'知道了',font=F['button'],fill=WHITE)
 else:
  q=smooth((t-14.7)/.7);caption(im,'时间，回到你的时区','Edge Time · Windows 桌面工具',q)
  paste_alpha(im,CAL,750,154,q)
 return im.convert('RGB')
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--output',default=str(ROOT/'EdgeTime-demo.mp4'));args=parser.parse_args();ff=shutil.which('ffmpeg')
 if not ff:raise SystemExit('FFmpeg is required on PATH.')
 cmd=[ff,'-y','-loglevel','error','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','-','-an','-c:v','libx264','-preset','medium','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',args.output]
 proc=subprocess.Popen(cmd,stdin=subprocess.PIPE)
 try:
  for n in range(FPS*DURATION):proc.stdin.write(frame(n/FPS).tobytes())
 finally:proc.stdin.close()
 if proc.wait():raise SystemExit('FFmpeg failed')
 print(args.output)
if __name__=='__main__':main()
