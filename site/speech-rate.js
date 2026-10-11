// Windows System.Speech rate is an integer [-10, 10], not a 1x/2x multiplier.
export function normalizeSpeechRate(value) {
  const number=Number(value);
  return Number.isFinite(number)?Math.max(-10,Math.min(10,Math.round(number))):0;
}

export function formatSpeechRate(value) {
  const rate=normalizeSpeechRate(value);
  if(rate===0)return '標準（0）';
  if(rate<0)return '遅い（'+rate+'）';
  return '速い（＋'+rate+'）';
}

export function speechRateAccessibilityText(value) {
  const rate=normalizeSpeechRate(value);
  if(rate===0)return '標準速度、0';
  return rate<0?'標準より遅い、マイナス'+Math.abs(rate):'標準より速い、プラス'+rate;
}
