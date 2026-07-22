using NAudio.Wave;

namespace CryoChaos.Services;

internal sealed class AudioDspProcessor
{
    private const float WetGain = 3.4f;
    private readonly GameAudioEffectMode _mode;
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly float[] _delay;
    private readonly float[] _reverbA;
    private readonly float[] _reverbB;
    private readonly List<float> _reverseBlock = [];
    private int _delayPosition;
    private int _reverbAPosition;
    private int _reverbBPosition;
    private float[] _lowPass;
    private double _pitchPhase;

    public AudioDspProcessor(GameAudioEffectMode mode, WaveFormat format)
    {
        if (format.BitsPerSample != 16)
        {
            throw new NotSupportedException("The game audio DSP currently requires 16-bit PCM.");
        }

        _mode = mode;
        _channels = format.Channels;
        _sampleRate = format.SampleRate;
        _delay = new float[(int)(_sampleRate * 0.28) * _channels];
        _reverbA = new float[(int)(_sampleRate * 0.043) * _channels];
        _reverbB = new float[(int)(_sampleRate * 0.071) * _channels];
        _lowPass = new float[_channels];
    }

    public byte[] Process(byte[] input)
    {
        float[] samples = new float[input.Length / sizeof(short)];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = BitConverter.ToInt16(input, index * 2) / 32768f;
        }

        float[] effected = _mode switch
        {
            GameAudioEffectMode.Echo => Echo(samples),
            GameAudioEffectMode.Reverse => Reverse(samples),
            GameAudioEffectMode.Underwater => Underwater(samples),
            GameAudioEffectMode.PitchUp => Pitch(samples, 1.38),
            GameAudioEffectMode.PitchDown => Pitch(samples, 0.72),
            GameAudioEffectMode.Reverb => Reverb(samples),
            _ => samples
        };

        byte[] output = new byte[effected.Length * sizeof(short)];
        for (int index = 0; index < effected.Length; index++)
        {
            // A soft limiter keeps the compensated wet signal from clipping.
            float limited = MathF.Tanh(effected[index] * WetGain * 0.82f);
            short value = (short)Math.Clamp(
                limited * short.MaxValue,
                short.MinValue,
                short.MaxValue);
            output[index * 2] = (byte)value;
            output[index * 2 + 1] = (byte)(value >> 8);
        }

        return output;
    }

    private float[] Echo(float[] input)
    {
        float[] output = new float[input.Length];
        for (int index = 0; index < input.Length; index++)
        {
            float delayed = _delay[_delayPosition];
            output[index] = input[index] * 0.72f + delayed * 0.72f;
            _delay[_delayPosition] = input[index] + delayed * 0.38f;
            _delayPosition = (_delayPosition + 1) % _delay.Length;
        }
        return output;
    }

    private float[] Reverse(float[] input)
    {
        // Reverse short 180 ms windows. The small intentional latency makes a
        // genuine reverse stream possible without buffering the full effect.
        int blockSamples = (int)(_sampleRate * 0.18) * _channels;
        _reverseBlock.AddRange(input);
        float[] output = new float[input.Length];
        if (_reverseBlock.Count < blockSamples)
        {
            return output;
        }

        int available = Math.Min(output.Length, _reverseBlock.Count);
        for (int frameOffset = 0; frameOffset < available; frameOffset += _channels)
        {
            int sourceFrame = _reverseBlock.Count - _channels - frameOffset;
            for (int channel = 0; channel < _channels; channel++)
            {
                if (frameOffset + channel < output.Length)
                {
                    output[frameOffset + channel] = _reverseBlock[sourceFrame + channel];
                }
            }
        }
        _reverseBlock.RemoveRange(Math.Max(0, _reverseBlock.Count - available), available);
        return output;
    }

    private float[] Underwater(float[] input)
    {
        float[] output = new float[input.Length];
        float alpha = LowPassAlpha(720f);
        for (int index = 0; index < input.Length; index++)
        {
            int channel = index % _channels;
            _lowPass[channel] += alpha * (input[index] - _lowPass[channel]);
            output[index] = _lowPass[channel] * 0.9f;
        }
        return output;
    }

    private float[] Pitch(float[] input, double ratio)
    {
        float[] output = new float[input.Length];
        int frames = input.Length / _channels;
        if (frames < 2) return output;

        for (int frame = 0; frame < frames; frame++)
        {
            double position = (_pitchPhase + frame * ratio) % (frames - 1);
            int first = (int)position;
            int second = first + 1;
            float fraction = (float)(position - first);
            for (int channel = 0; channel < _channels; channel++)
            {
                float a = input[first * _channels + channel];
                float b = input[second * _channels + channel];
                output[frame * _channels + channel] = a + (b - a) * fraction;
            }
        }
        _pitchPhase = (_pitchPhase + frames * ratio) % (frames - 1);
        return output;
    }

    private float[] Reverb(float[] input)
    {
        float[] output = new float[input.Length];
        for (int index = 0; index < input.Length; index++)
        {
            float a = _reverbA[_reverbAPosition];
            float b = _reverbB[_reverbBPosition];
            output[index] = input[index] * 0.58f + a * 0.36f + b * 0.3f;
            _reverbA[_reverbAPosition] = input[index] + a * 0.56f;
            _reverbB[_reverbBPosition] = input[index] + b * 0.43f + a * 0.16f;
            _reverbAPosition = (_reverbAPosition + 1) % _reverbA.Length;
            _reverbBPosition = (_reverbBPosition + 1) % _reverbB.Length;
        }
        return output;
    }

    private float LowPassAlpha(float cutoff)
    {
        float dt = 1f / _sampleRate;
        float rc = 1f / (2f * MathF.PI * cutoff);
        return dt / (rc + dt);
    }
}
