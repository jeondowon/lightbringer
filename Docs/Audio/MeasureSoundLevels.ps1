$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$invariant = [Globalization.CultureInfo]::InvariantCulture
# Measure PCM samples directly. These are unweighted dBFS measurements, not LUFS or acoustic SPL.
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
public static class LightbringerWaveLevels {
    public static double[] Measure(string path) {
        using (var r = new BinaryReader(File.OpenRead(path))) {
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "RIFF") throw new Exception("Not RIFF: " + path);
            r.ReadUInt32();
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "WAVE") throw new Exception("Not WAVE: " + path);
            int format = 0, channels = 0, rate = 0, bits = 0;
            byte[] data = null;
            while (r.BaseStream.Position + 8 <= r.BaseStream.Length) {
                string id = Encoding.ASCII.GetString(r.ReadBytes(4));
                int size = checked((int)r.ReadUInt32());
                long end = r.BaseStream.Position + size;
                if (end > r.BaseStream.Length) throw new Exception("Truncated WAV: " + path);
                if (id == "fmt ") {
                    format = r.ReadUInt16(); channels = r.ReadUInt16(); rate = r.ReadInt32();
                    r.ReadInt32(); r.ReadUInt16(); bits = r.ReadUInt16();
                } else if (id == "data") data = r.ReadBytes(size);
                r.BaseStream.Position = end + (size % 2);
            }
            if (format != 1 || channels != 1 || bits != 16 || rate <= 0 || data == null || data.Length < 2 || data.Length % 2 != 0)
                throw new Exception("Expected nonempty mono PCM16 WAV: " + path);
            int count = data.Length / 2, window = Math.Min(count, (int)Math.Round(rate * 0.1));
            double sum = 0, peak = 0, rolling = 0, loudest = 0;
            var squares = new double[count];
            for (int i = 0; i < count; i++) {
                double value = (short)(data[i * 2] | (data[i * 2 + 1] << 8)) / 32768.0;
                peak = Math.Max(peak, Math.Abs(value));
                squares[i] = value * value; sum += squares[i]; rolling += squares[i];
                if (i >= window) rolling -= squares[i - window];
                if (i >= window - 1) loudest = Math.Max(loudest, rolling);
            }
            return new[] { count / (double)rate, 20 * Math.Log10(peak),
                10 * Math.Log10(sum / count), 10 * Math.Log10(loudest / window) };
        }
    }
}
'@

# Clips now share an offline RMS reference; confirm playback uses only the user's slider.
$code = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Audio/SoundEffects.cs') -Raw
$volumeMatch = [regex]::Match($code, 'GetFloat\(VolumeKey,\s*([\d.]+)f\)')
if (!$volumeMatch.Success -or $code -notmatch 'source\.volume = volume;' -or $code -match 'Gain\(cue\)') {
    throw 'Runtime volume definition changed; update measurement parser.'
}
$masterVolume = [double]::Parse($volumeMatch.Groups[1].Value, $invariant)
$labels = @{
    ArrowHit='화살 명중'; ArrowRelease='활 발사'; BossImpact='보스 기술'; BossWarning='보스 경고'
    Click='버튼 선택'; Defeat='패배'; Denied='사용 불가'; FireBreath='드래곤 화염'
    HeavyImpact='강한 충격'; HeroHurt='영웅 피격'; HeroLightStaff='영웅 지팡이 공격'
    MageFire='마법사 불 마법'; MeleeHit='근접 타격 1'; MeleeHit_2='근접 타격 2'; MeleeHit_3='근접 타격 3'
    PriestHeal='사제 회복'; Summon='병력 소환'; TroopDeathFemale='여성 병력 사망'; TroopDeathMale='남성 병력 사망'; Victory='승리'
    ShamanWater='적 주술사 물 마법'; EnemyDeath='적 병력 사망'; BossSummon='군주 병력 소환'; HeroHeal='영웅 치유 지팡이'
}
$rows = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Audio/Resources/SoundEffects') -Filter '*.wav' | Sort-Object Name | ForEach-Object {
    $stats = [LightbringerWaveLevels]::Measure($_.FullName)
    $cue = $_.BaseName -replace '_\d+$', ''
    if ($code -notmatch "\b$cue\b") { throw "Unknown cue: $cue" }
    $gain = 1.0
    $attenuation = 20 * [Math]::Log10($gain * $masterVolume)
    [pscustomobject][ordered]@{
        File=$_.Name; Label=$labels[$_.BaseName]; Seconds=[Math]::Round($stats[0],3)
        PeakDbfs=[Math]::Round($stats[1],2); RmsDbfs=[Math]::Round($stats[2],2)
        Max100msRmsDbfs=[Math]::Round($stats[3],2); CueGain=$gain; DefaultVolume=$masterVolume
        GamePeakDbfs=[Math]::Round($stats[1]+$attenuation,2)
        GameRmsDbfs=[Math]::Round($stats[2]+$attenuation,2)
        GameMax100msRmsDbfs=[Math]::Round($stats[3]+$attenuation,2)
    }
})
$rows | Export-Csv (Join-Path $PSScriptRoot 'SoundLevels.csv') -NoTypeInformation -Encoding UTF8
$data = ConvertTo-Json -InputObject $rows -Depth 3 -Compress
$timestamp = [DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss') + ' UTC'
$html = @'
<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Lightbringer 효과음 음량 측정</title><style>
@font-face{font-family:Nanum;src:url('../../Assets/UI/Fonts/NanumGothic-Regular.ttf')}
body{font-family:Nanum,sans-serif;background:#121c21;color:#ede9db;max-width:1300px;margin:auto;padding:24px;line-height:1.65}
a{color:#d5c292}table{border-collapse:collapse;width:100%;white-space:nowrap}td,th{padding:9px;border-bottom:1px solid #38484c;text-align:right}td:first-child,th:first-child{text-align:left}
th{background:#24373d}audio{width:230px;height:34px}small,p{color:#b6c2c3}.scroll{overflow-x:auto}select,button{font:inherit;padding:6px;margin:8px}label{display:inline-block}
</style></head><body><h1>효과음 음량 측정</h1>
<p>측정: __TIME__ · __COUNT__개 WAV. 수치는 dBFS이며 0에 가까울수록 큽니다. 스피커에서 나오는 실제 dB SPL이나 LUFS 측정값은 아닙니다.</p>
<p>파일 피크는 한 순간의 최대값, 전체 RMS는 무음과 잔향을 포함한 평균입니다. 최대 100ms RMS는 연속 0.1초 중 가장 큰 구간의 평균으로, 길이가 다른 짧은 효과음을 비교하는 보조 지표입니다. 음색에 따른 체감 차이는 청취로 확인해야 합니다.</p>
<p>모든 효과음을 최대 100ms RMS −20 dBFS로 보정하고, 순간 피크는 −1 dBFS 이하로 제한했습니다. 기존 효과음별 추가 감쇠를 제거하여 게임 슬라이더가 재생 음량을 직접 조절합니다.</p>
<p>게임 예상치는 기본 효과음 설정 __VOLUME__%를 반영했습니다. 사용자 저장 설정·거리 감쇠·동시 재생·오디오 출력 장치는 반영하지 않았습니다. 전체 파일 평균은 잔향과 길이에 따라 다릅니다.</p>
<label>미리듣기 기준 <select id="mode"><option value="game">게임 기본 볼륨 반영</option><option value="file">파일 원래 볼륨</option></select></label>
<label>추가 미리듣기 음량 <input id="volume" type="range" min="0" max="100" value="70"><output id="value">70%</output></label>
<button id="stop">정지</button><p>같은 미리듣기 음량으로 비교하세요. 파일 원래 볼륨으로 전환하면 더 크게 재생됩니다.</p>
<div class="scroll"><table><thead><tr><th>효과음</th><th>길이(초)</th><th>파일 피크</th><th>파일 전체 RMS</th><th>파일 최대 100ms RMS</th><th>게임 최대 100ms RMS</th><th>미리듣기</th></tr></thead><tbody id="rows"></tbody></table></div>
<p><a href="SoundLevels.csv">현재 측정값 CSV</a> · <a href="SoundLevelsBeforeNormalization.csv">보정 전 측정값 CSV</a> · <a href="SoundEffectsPreview.html">전체 미리듣기</a></p>
<script>
const data=__DATA__;
const players=[], mode=document.getElementById('mode'), volume=document.getElementById('volume');
function apply(){for(const [p,r] of players)p.volume=Number(volume.value)/100*(mode.value==='game'?r.CueGain*r.DefaultVolume:1);document.getElementById('value').textContent=volume.value+'%';}
function stop(except){for(const [p] of players)if(p!==except){p.pause();if(p.readyState)p.currentTime=0;}}
for(const r of [...data].sort((a,b)=>b.GameMax100msRmsDbfs-a.GameMax100msRmsDbfs)){
 const tr=document.createElement('tr');
 for(const v of [r.Label,r.Seconds.toFixed(3),r.PeakDbfs.toFixed(1),r.RmsDbfs.toFixed(1),r.Max100msRmsDbfs.toFixed(1),r.GameMax100msRmsDbfs.toFixed(1)]){const td=document.createElement('td');td.textContent=v;tr.append(td);}
 const td=document.createElement('td'), p=document.createElement('audio');p.controls=true;p.preload='none';p.src='../../Assets/Audio/Resources/SoundEffects/'+r.File;p.setAttribute('aria-label',r.Label);p.addEventListener('play',()=>stop(p));td.append(p);tr.append(td);document.getElementById('rows').append(tr);players.push([p,r]);
}
mode.addEventListener('change',apply);volume.addEventListener('input',apply);document.getElementById('stop').addEventListener('click',()=>stop());apply();
</script></body></html>
'@
$html.Replace('__DATA__',$data).Replace('__TIME__',$timestamp).Replace('__COUNT__',[string]$rows.Count).Replace('__VOLUME__',[string]($masterVolume*100)) | Set-Content (Join-Path $PSScriptRoot 'SoundLevels.html') -Encoding UTF8
$rows | Sort-Object GameMax100msRmsDbfs -Descending | Format-Table Label,PeakDbfs,RmsDbfs,Max100msRmsDbfs,CueGain,GameMax100msRmsDbfs -AutoSize
