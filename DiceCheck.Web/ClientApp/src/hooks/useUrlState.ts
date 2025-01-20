import { useState, useCallback } from 'react';
import { DiceConfig, Condition } from '../types/dice';

export function useUrlState() {
  const [searchParams, setSearchParams] = useState<URLSearchParams>(
    new URLSearchParams(window.location.search)
  );

  const updateUrl = useCallback((config: DiceConfig, conditions: Condition[]) => {
    const params = new URLSearchParams();
    params.set('sides', config.sides.toString());
    params.set('numberOfDice', config.numberOfDice.toString());
    
    conditions.forEach(condition => {
      params.append('conditionType', condition.type);
      params.append('conditionValue', condition.value);
      if (condition.type === 'countMatching' && condition.count) {
        params.append('conditionCount', condition.count);
      }
    });

    window.history.replaceState(
      {},
      '',
      `${window.location.pathname}?${params.toString()}`
    );
    setSearchParams(params);
  }, []);

  const loadFromUrl = useCallback(() => {
    const params = new URLSearchParams(window.location.search);
    
    const config: DiceConfig = {
      sides: parseInt(params.get('sides') || '6'),
      numberOfDice: parseInt(params.get('numberOfDice') || '2')
    };

    const types = params.getAll('conditionType');
    const values = params.getAll('conditionValue');
    const counts = params.getAll('conditionCount');

    const conditions: Condition[] = types.map((type, index) => {
      const condition: Condition = {
        type: type as Condition['type'],
        value: values[index]
      };
      if (type === 'countMatching') {
        condition.count = counts[index];
      }
      return condition;
    });

    return { config, conditions };
  }, []);

  return { searchParams, updateUrl, loadFromUrl };
}
