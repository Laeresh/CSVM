"""Monte Carlo of the decoded plane-wobble chain (docs/org/shakes.md, "The rendered rotation"):
FUN_0042be10's kick, FUN_0042bec0's 1/150 s integrator over the triple, the unhalved quaternion
and the YXZ readback, at 60 Hz with the shipped shakes.zrd constants. Independent of the C# code.
Prints rendered RMS and mean peak per event, and the former port's roll-only figures.
"""
import numpy as np, math
rng = np.random.default_rng(1)
H=1/150; DT=1/60
def block(freq,damp,saw):
    return dict(f=freq,d=damp,s=saw,x=np.zeros(3),v=np.zeros(3))
def kick(b,mag,w3=(1.2,1.2,2.5)):
    step=mag*b["f"]*(4 if b["s"] else 2*math.pi)
    b["v"]+= (rng.random(3)-0.5)*step*np.array(w3)
def adv(b,dt):
    left=dt
    while left>1e-7:
        h=min(H,left); x=b["x"]; v=b["v"]
        if not b["s"]:
            w=b["f"]*2*math.pi; v-= (b["d"]*v+w*w*x)*h
        elif x.dot(v)>0 and np.linalg.norm(v)<np.linalg.norm(x)*b["f"]*4:
            b["v"]=v=-4*b["f"]*math.exp(-b["d"]*0.5/b["f"])*x
        b["x"]=x+b["v"]*h; left-=h
def rot(s):
    L=np.linalg.norm(s)
    if L==0: return np.zeros(3)
    w=math.cos(L); k=math.sin(L)/L; x,y,z=s*k
    m1=2*(x*y+w*z); m4=1-2*(z*z+x*x); m6=2*(y*w+x*z); m7=2*(z*y-x*w); m8=1-2*(x*x+y*y)
    return np.array([math.asin(-m7),math.atan2(m6,m8),math.atan2(m1,m4)])
def run(name,setup,ticks,ev):
    out=[]
    for trial in range(200):
        b=setup()
        tr=[]
        for t in range(ticks):
            ev(b,t)
            adv(b,DT); tr.append(rot(b["x"]))
        out.append(np.array(tr))
    a=np.stack(out)
    rms=np.sqrt((a**2).mean(axis=(0,1))); pk=np.abs(a).max(axis=1).mean(axis=0)
    print(f"{name}: RMS pitch/yaw/roll {rms[0]:.2e} {rms[1]:.2e} {rms[2]:.2e} rad; mean peak {pk[0]:.2e} {pk[1]:.2e} {pk[2]:.2e}")
run("fire wep40 3 s @8/s", lambda: block(15,12.5,1), 180, lambda b,t: kick(b,7e-5*40) if t%8==0 else None)
run("dive 1.4x", lambda: block(15,12.5,1), 180, lambda b,t: kick(b,0.4/70))
run("nitro engage", lambda: block(4,3,1), 120, lambda b,t: kick(b,0.05) if t==0 else None)
run("cannon hit 40cal", lambda: block(2.2,14,0), 60, lambda b,t: kick(b,40*5e-4) if t==0 else None)
run("HE rocket 40/60 direct", lambda: block(2.2,6.5,0), 90, lambda b,t: kick(b,60*1e-3*2) if t==0 else None)
run("contact saturated", lambda: block(2,4.5,0), 120, lambda b,t: kick(b,0.15) if t==0 else None)
# old port for comparison: roll only
def oldblock_run(name,setup,ticks,ev):
    out=[]
    for trial in range(200):
        b=setup(); tr=[]
        for t in range(ticks):
            ev(b,t); adv(b,DT); tr.append(b["x"][0])
        out.append(np.array(tr))
    a=np.stack(out); print(f"OLD {name}: roll RMS {np.sqrt((a**2).mean()):.2e}, mean peak {np.abs(a).max(axis=1).mean():.2e}")
oldblock_run("dive 1.4x", lambda: block(15,12.5,1), 180, lambda b,t: kick(b,0.4/70,(1.2,0,0)))
oldblock_run("nitro engage", lambda: block(4,3,1), 120, lambda b,t: kick(b,0.05,(1.2,0,0)) if t==0 else None)
# old fire walk
out=[]
for trial in range(200):
    w=0; tr=[]
    for t in range(180):
        if t%8==0: w+= (2*rng.random()-1)*7e-5*40*7.54
        w*=math.exp(-12.5*DT); tr.append(w)
    out.append(tr)
a=np.array(out); print(f"OLD fire walk: roll RMS {np.sqrt((a**2).mean()):.2e}, mean peak {np.abs(a).max(axis=1).mean():.2e}")
