import { DiceConfig as DiceConfigType } from '../../types/dice';

interface Props {
  config: DiceConfigType;
  onChange: (config: DiceConfigType) => void;
}

export function DiceConfig({ config, onChange }: Props) {
  const handleChange = (field: keyof DiceConfigType, value: string) => {
    onChange({
      ...config,
      [field]: parseInt(value)
    });
  };

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
      <div>
        <label className="block text-gray-700 text-sm font-bold mb-2" htmlFor="sides">
          Number of Sides:
        </label>
        <input
          type="number"
          id="sides"
          value={config.sides}
          onChange={(e) => handleChange('sides', e.target.value)}
          min="1"
          max="100"
          className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>
      <div>
        <label className="block text-gray-700 text-sm font-bold mb-2" htmlFor="numberOfDice">
          Number of Dice:
        </label>
        <input
          type="number"
          id="numberOfDice"
          value={config.numberOfDice}
          onChange={(e) => handleChange('numberOfDice', e.target.value)}
          min="1"
          max="100"
          className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>
    </div>
  );
}
