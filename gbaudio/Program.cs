using System;
using System.Diagnostics;
using System.IO;

namespace gbaudio
{
    class Program
    {
        public const uint ClockSpeed = 4194304; // 2^22 MHz
        const int SampleRate = 44100;
        const double ClocksPerSample = ClockSpeed / (double)SampleRate;

        static void Main(string[] args)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (WaveFileWriter writer = new WaveFileWriter(File.Open("output.wav", FileMode.Create), new WaveFormat(SampleRate, 16, 2)))
            {
                //double sampleCounter = 0.0;
                double updateClock = 0;
                uint updateCycles = 0;

                SoundController soundController = new SoundController();

                soundController._squareWave1.Frequency = 0x783;
                soundController._squareWave1.Init();
                for (uint i = 0; i < 351136; i += updateCycles)
                {
                    // run for one samples worth of updates
                    updateClock += ClocksPerSample;
                    updateCycles = (uint)updateClock;

                    soundController.Update(updateCycles);

                    short sample = (short)(soundController._squareWave1.Sample << 10);

                    writer.WriteSample(sample); // Left
                    writer.WriteSample(sample); // Right

                    updateClock -= updateCycles;
                }

                soundController._squareWave1.Frequency = 0x7C1;
                soundController._squareWave1.Init();

                for (uint i = 0; i < ClockSpeed - 351136; i += updateCycles)
                {
                    // run for one samples worth of updates
                    updateClock += ClocksPerSample;
                    updateCycles = (uint)updateClock;

                    soundController.Update(updateCycles);

                    short sample = (short)(soundController._squareWave1.Sample << 10);

                    writer.WriteSample(sample); // Left
                    writer.WriteSample(sample); // Right

                    updateClock -= updateCycles;
                }
            }

            stopwatch.Stop();

            Console.WriteLine("Generated in {0} ms", stopwatch.ElapsedMilliseconds);
        }
    }

    class SoundController
    {
        // frame sequencer clocked at 512 Hz
        private const uint FrameSequencerCycles = Program.ClockSpeed / 512;

        private uint _frameSequencerCounter = FrameSequencerCycles;
        private uint _frameSequencerFrame = 0;

        public SquareWave1 _squareWave1 = new SquareWave1();

        public void Update(uint cycles)
        {
            uint Min(uint x, uint y)
            {
                return Math.Min(x, y);
            }

            do
            {
                // run for the lowest of cycles, the next frame sequencer tick or the next sample
                var updateCycles = Min(cycles, _frameSequencerCounter);

                _squareWave1.Update(updateCycles);
                //_squareWave2.Update(updateCycles);
                //_wave.Update(updateCycles);
                //_noise.Update(updateCycles);

                if ((_frameSequencerCounter -= updateCycles) == 0)
                {
                    _frameSequencerCounter = FrameSequencerCycles;
                    UpdateFrameSequencer();
                }

                cycles -= updateCycles;
            } while (cycles > 0);
        }

        private void UpdateFrameSequencer()
        {
            _frameSequencerFrame = (_frameSequencerFrame + 1) % 8;

            switch (_frameSequencerFrame)
            {
                case 7:
                    _squareWave1.Envelope.Update();
                    break;
            }
        }
    }

    class SquareWave1
    {
        private static readonly int[][] Duty =
        {
            new int[] { 0,1,0,0,0,0,0,0 }, // 12.5%
            new int[] { 1,1,0,0,0,0,0,0 }, // 25%
            new int[] { 1,1,1,1,0,0,0,0 }, // 50%
            new int[] { 0,0,1,1,1,1,1,1 }  // 75%
        };

        private uint _frequency;
        private uint _frequencyCounter;

        private int _dutyCycle = 2; // TODO(david): remove default
        private int _phase = 0;
        private VolumeEnvelope _envelope;

        public int Sample { get; private set; }
        public uint Frequency { get => _frequency; set => _frequency = (2048 - value) * 4; }

        public VolumeEnvelope Envelope => _envelope;

        public SquareWave1()
        {
            _envelope = new VolumeEnvelope
            {
                InitialVolume = 0x0F,
                Period = 3,
                Direction = 0
            };
        }

        public void Init()
        {
            _envelope.Init();
            _frequencyCounter = _frequency;
        }

        public void Update(uint cycles)
        {
            do
            {
                // _frequencyCounter is the number of cycles still to run until the phase is updated
                // run for the lower value of that and cycles, then update
                uint runCycles = Math.Min(_frequencyCounter, cycles);

                if ((_frequencyCounter -= runCycles) == 0)
                {
                    _frequencyCounter = _frequency;
                    _phase = (_phase + 1) % 8;
                    Sample = Duty[_dutyCycle][_phase] * _envelope.Volume;
                }

                cycles -= runCycles;
            } while (cycles > 0);
        }
    }

    class VolumeEnvelope
    {
        private const int MaxEnvelopePeriod = 8;
        private const int MaxEnvelopeVolume = 15;

        private int _initialVolume;
        private int _direction;
        private int _period;

        private int _periodCounter;
        private int _volume;
        private bool _updating;

        public int InitialVolume { set => _initialVolume = value; }
        public int Period { set => _period = value; }
        public int Direction { set => _direction = value; }

        public int Volume => _volume;

        public void Init()
        {
            _volume = _initialVolume;
            _periodCounter = _period > 0 ? _period : MaxEnvelopePeriod;
            _updating = true;
        }

        public void Update()
        {
            if (_period != 0 && _updating)
            {
                if (--_periodCounter == 0)
                {
                    _periodCounter = _period;

                    int delta = _direction == 1 ? 1 : -1;
                    _volume += delta;

                    if (_volume == MaxEnvelopeVolume || _volume == 0)
                    {
                        _updating = false;
                    }
                }
            }
        }
    }
}
