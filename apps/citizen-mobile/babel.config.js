module.exports = function (api) {
  api.cache(true);
  const isTest = process.env.NODE_ENV === 'test';
  return {
    presets: ['babel-preset-expo'],
    // Reanimated's plugin pulls in react-native-worklets/plugin which isn't
    // needed under Jest and fails to resolve in the test environment.
    plugins: isTest ? [] : ['react-native-reanimated/plugin'],
  };
};
