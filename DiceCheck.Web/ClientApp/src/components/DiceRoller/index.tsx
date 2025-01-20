import { useState } from 'react';
import { DiceConfig as DiceConfigType, Condition, RollResult } from '../../types/dice';
import { diceApi } from '../../services/diceApi';
import { useUrlState } from '../../hooks/useUrlState';
import { DiceConfig } from './DiceConfig';
import { Conditions } from './Conditions';
import { Results } from './Results';

export function DiceRoller() {
  const { updateUrl, loadFromUrl } = useUrlState();
  const initialState = loadFromUrl();
  
  const [diceConfig, setDiceConfig] = useState<DiceConfigType>(initialState.config);
  const [conditions, setConditions] = useState<Condition[]>(
    initialState.conditions?.length > 0 ? initialState.conditions : []
  );
  const [results, setResults] = useState<RollResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleConfigChange = (newConfig: DiceConfigType) => {
    setDiceConfig(newConfig);
    updateUrl(newConfig, conditions);
  };

  const handleConditionsChange = (newConditions: Condition[]) => {
    setConditions(newConditions);
    updateUrl(diceConfig, newConditions);
  };

  const handleRoll = async () => {
    try {
      if (diceConfig.sides <= 0) {
        setError("Number of sides must be positive");
        return;
      }
      if (diceConfig.numberOfDice <= 0) {
        setError("Number of dice must be positive");
        return;
      }
      const result = await diceApi.rollDice({
        ...diceConfig,
        conditions
      });
      setResults(result);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
      setResults(null);
    }
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-4xl font-bold text-center mb-8 text-gray-800">Dice Roller</h1>
      
      <div className="max-w-2xl mx-auto bg-white rounded-lg shadow-lg p-6">
        <DiceConfig config={diceConfig} onChange={handleConfigChange} />
        <Conditions conditions={conditions} onUpdate={handleConditionsChange} />

        <button
          onClick={handleRoll}
          className="w-full px-6 py-3 bg-blue-500 text-white rounded-lg hover:bg-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Roll Dice
        </button>

        <Results results={results} />

        {error && (
          <div className="mt-4 p-4 bg-red-100 text-red-800 rounded-lg" data-testid="error-message">
            {error}
          </div>
        )}
      </div>
    </div>
  );
}
