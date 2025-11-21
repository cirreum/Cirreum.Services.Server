namespace Cirreum.FileSystem;

using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

sealed class CsvFileReader : ICsvFileReader {

	public CsvFileReader() {

	}

	public IEnumerable<TRecord> ReadCsvFile<TRecord>(string fileName, CsvOptions options) {

		void missingFieldCallback(MissingFieldFoundArgs args) {
			if (options.IgnoreMissingFields) {
				return;
			}
			throw new MissingFieldException(args.Context);
		}
		var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
			HasHeaderRecord = options.HasHeaderRecord,
			Delimiter = options.Delimiter,
			MissingFieldFound = missingFieldCallback
		};

		using (var reader = new StreamReader(fileName)) {

			using (var csv = new CsvReader(reader, config)) {

				foreach (var record in csv.GetRecords<TRecord>()) {
					yield return record;
				}

			}

		}

	}

	public IEnumerable<TRecord> ReadCsvFile<TRecord, TClassMap>(string fileName, CsvOptions options) where TClassMap : ClassMap<TRecord> {

		void missingFieldCallback(MissingFieldFoundArgs args) {
			if (options.IgnoreMissingFields) {
				return;
			}
			throw new MissingFieldException(args.Context);
		}
		var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
			HasHeaderRecord = options.HasHeaderRecord,
			Delimiter = options.Delimiter,
			MissingFieldFound = missingFieldCallback
		};

		using (var reader = new StreamReader(fileName)) {

			using (var csv = new CsvReader(reader, config)) {

				csv.Context.RegisterClassMap<TClassMap>();

				foreach (var record in csv.GetRecords<TRecord>()) {
					yield return record;
				}

			}

		}

	}

}