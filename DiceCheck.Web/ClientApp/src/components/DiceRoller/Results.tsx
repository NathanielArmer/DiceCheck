import { RollResult } from '../../types/dice';

interface Props {
  results: RollResult | null;
}

export function Results({ results }: Props) {
  if (!results) return null;

  return (
    <div className="mt-8">
      <h2 className="text-2xl font-bold mb-4 text-gray-800">Results</h2>
      <div className="grid grid-cols-3 md:grid-cols-5 gap-4">
        {results.values.map((value, index) => (
          <div key={index} className="dice-value text-center p-4 bg-gray-100 rounded-lg text-xl font-bold" data-testid="dice-value">
            {value}
          </div>
        ))}
      </div>
      <div className="mt-4 text-xl font-bold text-gray-800" data-testid="sum">
        Sum: {results.sum}
      </div>
      {results.conditions?.length > 0 && (
        <div className="mt-4 space-y-2">
          {results.conditions.map((result, index) => (
            <div
              key={index}
              className={`condition-result p-2 rounded-lg ${
                result.satisfied ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
              }`}
              data-testid="condition-result"
            >
              {result.condition}: {result.satisfied ? 'Satisfied' : 'Not Satisfied'}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
