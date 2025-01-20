import axios from 'axios';
import { DiceConfig, Condition, RollResult, RollRequest } from '../types/dice';

const api = axios.create({
  baseURL: '/api'
});

export const diceApi = {
  rollDice: async (config: DiceConfig & { conditions: Condition[] }): Promise<RollResult> => {
    const request: RollRequest = {
      sides: config.sides,
      numberOfDice: config.numberOfDice,
      conditions: config.conditions || []
    };
    const { data } = await api.post<RollResult>('/roll', request);
    return data;
  }
};
