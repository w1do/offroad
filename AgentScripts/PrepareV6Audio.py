from pathlib import Path
import numpy as np,soundfile as sf
from scipy import signal
root=Path('SourceAssets/Audio/V6');out=Path('Assets/Game/Audio/PajeroV6');out.mkdir(exist_ok=True)
def read(sid):
 x,sr=sf.read(root/(sid+'.mp3'));return (x.mean(axis=1) if x.ndim>1 else x),sr
def stable(x,sr,start,length,freq,band):
 y=x[int(start*sr):int((start+length)*sr)];y=signal.sosfiltfilt(signal.butter(2,28,fs=sr,btype='high',output='sos'),y)
 f,t,z=signal.stft(y,fs=sr,nperseg=8192,noverlap=7168);ids=np.where((f>=band[0])&(f<=band[1]))[0];ridge=f[ids[np.argmax(abs(z[ids]),axis=0)]];ridge=signal.medfilt(ridge,5)
 phase=np.cumsum(np.interp(np.arange(len(y))/sr,t,ridge)/freq);samples=np.arange(phase[-1]);positions=np.interp(samples,phase,np.arange(len(y)));y=np.interp(positions,np.arange(len(y)),y)
 # Constant-power overlap at loop boundary; no silent fades or start/stop transients.
 overlap=min(int(.12*sr),len(y)//6);a=np.linspace(0,1,overlap);join=y[-overlap:]*(1-a)+y[:overlap]*a;y=np.concatenate([join,y[overlap:-overlap]])
 return y
idle,sr=read('756000');drive,_=read('754775')
manifest=[]
for name,rpm,start,length,band in [('Idle',850,18,4,(33,45)),('Low',1500,2.2,1.3,(60,100)),('Mid',3000,5.8,1.0,(110,155)),('High',4500,8.6,1.1,(180,250)),('Redline',6500,20,1.5,(300,360))]:
 source=idle if name=='Idle' else drive;y=stable(source,sr,start,length,rpm/20,band)
 for load in ['Coast','Load']:
  z=y.copy()
  if load=='Coast':z=signal.sosfiltfilt(signal.butter(2,1400 if name!='Idle' else 2600,fs=sr,output='sos'),z)
  rms=np.sqrt(np.mean(z*z));z=z*.14/max(.0001,rms);z=z*min(1,.88/max(abs(z)));z=signal.resample_poly(z,147,160,padtype="wrap");sf.write(out/(name+load+'.wav'),z,44100,subtype='PCM_16');manifest.append(f'{name}{load}: reference {rpm} RPM; source {"756000" if name=="Idle" else "754775"}, {start}..{start+length}s; duration {len(z)/44100:.3f}s; peak {max(abs(z)):.3f}; seam {abs(z[-1]-z[0]):.5f}')
x=idle[:int(2.8*sr)];x=x*min(6,.85/max(abs(x)));fade=int(.03*sr);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade);sf.write(out/'Start.wav',signal.resample_poly(x,147,160),44100,subtype='PCM_16')
(out/'SOURCES.txt').write_text('''Recorded gasoline V6 engine basis, not a verified Mitsubishi 6G72/6G74 recording.
Author: EvanBoyerman (Evan Boyerman), Freesound.
License: Creative Commons Attribution 4.0 International
https://creativecommons.org/licenses/by/4.0/
Source recordings:
https://freesound.org/people/EvanBoyerman/sounds/754775/
Sports Car Accelerating/Speeding Up Fast - Exhaust (3.2L V6 V-Tec)
https://freesound.org/people/EvanBoyerman/sounds/756000/
Car Ignition Starting with Idle and Stop (Engine and Exhaust Mix)
Downloaded public HQ MP3 versions linked by the source pages, 30 October 2026.
Changes: mono conversion, high-pass cleanup, short segment extraction,
pitch stabilization to approximate RPM bands, overlapped loop seams,
level matching, low-pass coast variants, 44.1kHz PCM16 WAV export.
The coast variants are filtered recordings, not separate off-load takes.
RPM references are game calibration estimates, not measured tachometer data.
Credit in distributed game: V6 engine recordings by EvanBoyerman / Freesound,
CC BY 4.0, edited for this game. No endorsement implied.

'''+ '\n'.join(manifest),encoding='utf8')
print('\n'.join(manifest))

