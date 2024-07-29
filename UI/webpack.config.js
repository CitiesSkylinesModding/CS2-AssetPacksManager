import path from 'node:path';
import mod from './mod.json' with { type: 'json' };
import TerserPlugin from 'terser-webpack-plugin';

const userDataPath = process.env.CSII_USERDATAPATH;

if (!userDataPath) {
    throw 'CSII_USERDATAPATH environment variable is not set, ensure the CSII Modding Toolchain is installed correctly';
}

const outputDir = `${userDataPath}\\Mods\\${mod.id}`;

const banner = `
 * Cities: Skylines II UI Module
 *
 * Id: ${mod.id}
 * Author: ${mod.author}
 * Version: ${mod.version}
 * Dependencies: ${mod.dependencies.join(',')}
`;

export default {
    mode: 'production',
    stats: 'none',
    entry: {
        [mod.id]: path.join(import.meta.dirname, 'index.tsx'),
    },
    externalsType: 'window',
    externals: {
        react: 'React',
        'react-dom': 'ReactDOM',
        'cs2/modding': 'cs2/modding',
        'cs2/api': 'cs2/api',
        'cs2/bindings': 'cs2/bindings',
        'cs2/l10n': 'cs2/l10n',
        'cs2/ui': 'cs2/ui',
        'cs2/input': 'cs2/input',
        'cs2/utils': 'cs2/utils',
        'cohtml/cohtml': 'cohtml/cohtml',
    },
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: 'ts-loader',
                exclude: /node_modules/,
            }
        ],
    },
    resolve: {
        extensions: ['.tsx', '.ts', '.js'],
        modules: ['node_modules', path.join(import.meta.dirname, 'src')],
        alias: {
            'mod.json': path.resolve(import.meta.dirname, 'mod.json'),
        },
    },
    output: {
        path: path.resolve(import.meta.dirname, outputDir),
        library: {
            type: 'module',
        },
        publicPath: `coui://ui-mods/`,
    },
    optimization: {
        minimize: true,
        minimizer: [
            new TerserPlugin({
                extractComments: {
                    banner: () => banner,
                },
            }),
        ],
    },
    experiments: {
        outputModule: true,
    },
    plugins: [
        {
            apply(compiler) {
                compiler.hooks.done.tap('AfterDonePlugin', stats => {
                    console.info(stats.toString({ colors: process.stdout.isTTY }));
                    console.info(`Built UI mod @ ${outputDir}`);
                });
            }
        }
    ]
};
