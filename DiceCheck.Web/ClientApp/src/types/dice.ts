export interface DiceConfig {
  sides: number;
  numberOfDice: number;
}

export interface Condition {
  type: 'sumEquals' | 'sumGreaterThan' | 'sumLessThan' | 'atLeastOne' | 'all' | 'countMatching';
  value: string;
  count?: string;
}

export interface RollResult {
  values: number[];
  sum: number;
  conditions: Array<{
    condition: string;
    satisfied: boolean;
  }>;
}

export interface RollRequest {
  sides: number;
  numberOfDice: number;
  conditions: Condition[];
}
