AO QA staged test extension; existing ResearchPlay test count unchanged.

Outputs: noon-contact-ao-debug.png and ao-gpu-timing.txt.
GPU run bounded at4x(15warmup+60attempts)=300frames total. Unsupported/disabled timings produce explicit UNAVAILABLE; zero GPU values are not replaced with CPU or wall time. FrameTimingManager measures whole-frame GPU, not isolated camera/pass cost. Production camera renders normally into1280x720 HDR target; no repeated ReadPixels in timing loop. Full/half/full/half ordering balances temporal drift. Raw debug view and modes are restored in finally blocks.

Unity APIs checked against official6000.0 FrameTimingManager/FrameTiming docs. No Unity compile/run performed here. Parent integrates staged existing file and runs ResearchPlay.
